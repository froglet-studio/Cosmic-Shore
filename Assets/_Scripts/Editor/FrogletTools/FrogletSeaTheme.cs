using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Editor.Froglet
{
    /// <summary>
    /// The game's colour sets as editor themes, and the kit a themed Froglet window draws with: the menu's sea lines
    /// behind everything, panels lit like the game's prisms (a faint rim over the face) with the style guide's corner
    /// sliver (top-left and bottom-right cut), and the style guide's meanings — the call to action is the set's lime,
    /// the selection and focus its Jade rim, danger its Fire (Docs/UI_STYLE_GUIDE.md §14: "a colour set changes how
    /// you see the HyperSea").
    ///
    /// <para>The colours are GENERATED from the shipped colour sets by <c>Tools/Build/author_editor_themes.py</c>
    /// (<c>FrogletSeaTheme.Themes.cs</c>, through the analytics console's <c>make_themes.py</c>, so both wear the
    /// same Photic). Never type a colour here; re-run the writer. FrogletTools > Build > Source Control is the first
    /// window to wear it (Shombith, 2026-11-06).</para>
    ///
    /// <para>Everything draws on <see cref="EventType.Repaint"/> only, in GUI space; the interactive pieces
    /// (<see cref="Button"/>, <see cref="Check"/>, <see cref="Tabs"/>) handle their own mouse events.</para>
    /// </summary>
    public static partial class FrogletSeaTheme
    {
        /// <summary>One theme's colours (screen colours, sRGB).</summary>
        public sealed class Palette
        {
            public string Id, Name, Tagline;
            public Color Ground, GroundDeep, Surface, SurfaceRaised, Line, Text, TextSoft, TextOff;
            public Color Cta, CtaFill, OnFill, Danger, Selection, Jade, Ruby, Gold, Blue, Data, DataText;
            public Color Rim, Face, Sea1, Sea2, Sea3, SeaWash, SeaGlow;
        }

        readonly struct SeaLayer
        {
            public readonly float Alpha, Width, Seconds;
            public SeaLayer(float alpha, float width, float seconds) { Alpha = alpha; Width = width; Seconds = seconds; }
        }

        /// <summary>How a button reads (Docs/UI_STYLE_GUIDE.md: one meaning per family).</summary>
        public enum ButtonKind
        {
            /// <summary>Lime fill: the one call to action (commit, push, create).</summary>
            Cta = 0,
            /// <summary>Jade outline: an action you can take back (pull, switch, open).</summary>
            Primary = 1,
            /// <summary>A quiet outline: housekeeping (refresh, fetch).</summary>
            Quiet = 2,
            /// <summary>Fire outline: throws something away.</summary>
            Danger = 3,
        }

        public const float SliverLarge = 14f, SliverSmall = 8f;
        const string ThemePref = "Froglet.EditorTheme";
        /// <summary>The theme a window opens in until someone picks another (Shombith asked for Photic).</summary>
        public const string DefaultTheme = "photic";
        const string LabelFontPath = "Assets/_Graphics/Fonts/ChakraPetch/ChakraPetch-SemiBold.ttf";
        const string BodyFontPath = "Assets/Unity Assests/TextMesh Pro/Resources/Fonts & Materials/ALDRICH-REGULAR.TTF";

        static Palette _current;
        static readonly Dictionary<string, Texture2D> _textures = new();
        static GUIStyle _title, _subtitle, _label, _labelStrong, _body, _bodySoft, _mono, _button, _field, _area, _pill, _tab;
        static Font _labelFont, _bodyFont, _monoFont;
        static bool _fontsLoaded;

        static Color Rgb(int hex) => new(((hex >> 16) & 0xFF) / 255f, ((hex >> 8) & 0xFF) / 255f, (hex & 0xFF) / 255f, 1f);

        public static IReadOnlyList<Palette> Themes => Generated;

        /// <summary>The theme in use (EditorPrefs <c>Froglet.EditorTheme</c>, else <see cref="DefaultTheme"/>).</summary>
        public static Palette Current
        {
            get
            {
                if (_current != null) return _current;
                var want = EditorPrefs.GetString(ThemePref, DefaultTheme);
                _current = Find(want) ?? Find(DefaultTheme) ?? Generated[0];
                return _current;
            }
        }

        public static void SetTheme(string id)
        {
            var p = Find(id);
            if (p == null) return;
            EditorPrefs.SetString(ThemePref, id);
            _current = p;
            ResetStyles();
        }

        static Palette Find(string id)
        {
            foreach (var p in Generated) if (p.Id == id) return p;
            return null;
        }

        public static Color WithAlpha(Color c, float a) => new(c.r, c.g, c.b, a);

        // ── Fonts and text styles ────────────────────────────────────────────────

        static void LoadFonts()
        {
            if (_fontsLoaded) return;
            _fontsLoaded = true;
            _labelFont = AssetDatabase.LoadAssetAtPath<Font>(LabelFontPath);
            _bodyFont = AssetDatabase.LoadAssetAtPath<Font>(BodyFontPath);
            try { _monoFont = Font.CreateDynamicFontFromOSFont(new[] { "Consolas", "Menlo", "DejaVu Sans Mono", "Courier New" }, 12); }
            catch (Exception) { _monoFont = null; }
        }

        static void ResetStyles()
        {
            _title = _subtitle = _label = _labelStrong = _body = _bodySoft = _mono = _button = _field = _area = _pill = _tab = null;
            _wrapped.Clear();
        }

        static GUIStyle Make(Font font, int size, Color color, FontStyle fontStyle = FontStyle.Normal, TextAnchor anchor = TextAnchor.MiddleLeft,
                             bool wrap = false, bool clip = true)
        {
            LoadFonts();
            var s = new GUIStyle(EditorStyles.label)
            {
                font = font,
                fontSize = size,
                fontStyle = font == null ? fontStyle : FontStyle.Normal,
                alignment = anchor,
                wordWrap = wrap,
                richText = false,
                clipping = clip ? TextClipping.Clip : TextClipping.Overflow,
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0),
            };
            s.normal.textColor = s.hover.textColor = s.active.textColor = s.focused.textColor = color;
            s.onNormal.textColor = color;
            return s;
        }

        /// <summary>The window title: the label face, large.</summary>
        public static GUIStyle Title => _title ??= Make(LabelFont, 18, Current.Text);
        public static GUIStyle Subtitle => _subtitle ??= Make(BodyFont, 11, Current.TextSoft, wrap: true);
        /// <summary>A small caps label (sections, field names).</summary>
        public static GUIStyle Label => _label ??= Make(LabelFont, 11, Current.TextSoft);
        public static GUIStyle LabelStrong => _labelStrong ??= Make(LabelFont, 12, Current.Text);
        public static GUIStyle Body => _body ??= Make(BodyFont, 12, Current.Text);
        public static GUIStyle BodySoft => _bodySoft ??= Make(BodyFont, 11, Current.TextSoft);
        static readonly Dictionary<Color, GUIStyle> _wrapped = new();

        /// <summary>Body text that wraps, in a colour (cached: OnGUI asks every frame).</summary>
        public static GUIStyle BodyWrapped(Color color)
        {
            if (!_wrapped.TryGetValue(color, out var s) || s == null) _wrapped[color] = s = Make(BodyFont, 12, color, wrap: true);
            return s;
        }
        public static GUIStyle Mono => _mono ??= Make(_monoFont, 12, Current.Text);
        static GUIStyle ButtonText => _button ??= Make(LabelFont, 12, Current.Text, FontStyle.Bold, TextAnchor.MiddleCenter);
        static GUIStyle PillText => _pill ??= Make(LabelFont, 10, Current.Text, FontStyle.Bold, TextAnchor.MiddleCenter);
        static GUIStyle TabText => _tab ??= Make(LabelFont, 12, Current.TextSoft, FontStyle.Bold, TextAnchor.MiddleCenter);

        static Font LabelFont { get { LoadFonts(); return _labelFont; } }
        static Font BodyFont { get { LoadFonts(); return _bodyFont; } }

        /// <summary>A text field on the deep ground with a hairline; the focus ring is the Jade selection.</summary>
        public static GUIStyle Field => _field ??= MakeField(false);
        public static GUIStyle Area => _area ??= MakeField(true);

        static GUIStyle MakeField(bool area)
        {
            var p = Current;
            // A fresh style, not a copy of EditorStyles.textField: the stock one carries high-DPI background images
            // that would draw over ours on a Retina / 4K screen.
            var s = new GUIStyle
            {
                font = BodyFont,
                fontSize = 12,
                border = new RectOffset(2, 2, 2, 2),
                padding = new RectOffset(8, 8, area ? 6 : 4, area ? 6 : 4),
                margin = new RectOffset(0, 0, 0, 0),
                alignment = area ? TextAnchor.UpperLeft : TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip,
                imagePosition = ImagePosition.TextOnly,
                wordWrap = area,
                richText = false,
            };
            var normal = Bordered(p.GroundDeep, p.Line);
            var focus = Bordered(p.GroundDeep, p.Selection);
            s.normal.background = s.hover.background = s.active.background = normal;
            s.focused.background = s.onFocused.background = focus;
            s.normal.textColor = s.hover.textColor = s.active.textColor = s.focused.textColor = p.Text;
            return s;
        }

        static Texture2D Bordered(Color fill, Color border)
        {
            var key = "b" + ColorUtility.ToHtmlStringRGBA(fill) + ColorUtility.ToHtmlStringRGBA(border);
            if (_textures.TryGetValue(key, out var t) && t) return t;
            t = new Texture2D(5, 5, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Point };
            for (var x = 0; x < 5; x++)
                for (var y = 0; y < 5; y++)
                    t.SetPixel(x, y, x == 0 || y == 0 || x == 4 || y == 4 ? border : fill);
            t.Apply();
            _textures[key] = t;
            return t;
        }

        // ── Shapes ───────────────────────────────────────────────────────────────

        static readonly Vector3[] _poly = new Vector3[6];
        static readonly Vector3[] _ring = new Vector3[7];

        static void SliverPoints(Rect r, float cut)
        {
            cut = Mathf.Min(cut, r.height * 0.5f, r.width * 0.5f);
            _poly[0] = new Vector3(r.x + cut, r.y);
            _poly[1] = new Vector3(r.xMax, r.y);
            _poly[2] = new Vector3(r.xMax, r.yMax - cut);
            _poly[3] = new Vector3(r.xMax - cut, r.yMax);
            _poly[4] = new Vector3(r.x, r.yMax);
            _poly[5] = new Vector3(r.x, r.y + cut);
            for (var i = 0; i < 6; i++) _ring[i] = _poly[i];
            _ring[6] = _poly[0];
        }

        static int _scrollDepth;

        /// <summary>
        /// A scroll view for themed content. Inside one, shapes draw with <c>EditorGUI.DrawRect</c> (square corners)
        /// instead of <c>Handles</c>: rects always clip to the view, and a list row must never paint outside its panel.
        /// </summary>
        public static Vector2 BeginScroll(Rect view, Vector2 scroll, Rect content)
        {
            _scrollDepth++;
            return GUI.BeginScrollView(view, scroll, content);
        }

        /// <summary>Call first in OnGUI: a scroll view an exception left open (ExitGUI) does not outlive its frame.</summary>
        public static void BeginFrame() => _scrollDepth = 0;

        public static void EndScroll()
        {
            GUI.EndScrollView();
            _scrollDepth = Mathf.Max(0, _scrollDepth - 1);
        }

        static void RectShape(Rect r, Color fill, Color border, float width)
        {
            if (fill.a > 0f) EditorGUI.DrawRect(r, fill);
            if (border.a <= 0f) return;
            var b = Mathf.Max(1f, Mathf.Round(width));
            EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, b), border);
            EditorGUI.DrawRect(new Rect(r.x, r.yMax - b, r.width, b), border);
            EditorGUI.DrawRect(new Rect(r.x, r.y, b, r.height), border);
            EditorGUI.DrawRect(new Rect(r.xMax - b, r.y, b, r.height), border);
        }

        /// <summary>A shape with the corner sliver: filled, then outlined (either colour may be clear).</summary>
        public static void Sliver(Rect r, Color fill, Color border, float cut = SliverSmall, float borderWidth = 1.4f)
        {
            if (Event.current.type != EventType.Repaint || r.width <= 0 || r.height <= 0) return;
            if (_scrollDepth > 0) { RectShape(r, fill, border, borderWidth); return; }
            SliverPoints(r, cut);
            var prev = Handles.color;
            if (fill.a > 0f)
            {
                Handles.color = fill;
                Handles.DrawAAConvexPolygon(_poly);
            }
            if (border.a > 0f)
            {
                Handles.color = border;
                Handles.DrawAAPolyLine(borderWidth, _ring);
            }
            Handles.color = prev;
        }

        public static void Fill(Rect r, Color c)
        {
            if (Event.current.type == EventType.Repaint) EditorGUI.DrawRect(r, c);
        }

        /// <summary>A panel, lit like the game's prisms: the face, a sheen from the top-left, the rim faint around it
        /// and brighter along the top.</summary>
        public static void Panel(Rect r, float cut = SliverLarge)
        {
            if (Event.current.type != EventType.Repaint) return;
            var p = Current;
            Sliver(r, WithAlpha(p.Surface, 0.88f), Color.clear, cut);
            // the sheen: the raised surface, faint, across the top-left
            var prev = Handles.color;
            Handles.color = WithAlpha(p.SurfaceRaised, 0.22f);
            var c = Mathf.Min(cut, r.height * 0.5f, r.width * 0.5f);
            Handles.DrawAAConvexPolygon(new Vector3(r.x + c, r.y), new Vector3(r.x + r.width * 0.42f, r.y),
                                        new Vector3(r.x, r.y + Mathf.Min(r.height, r.width * 0.42f)), new Vector3(r.x, r.y + c));
            Handles.color = prev;
            Sliver(r, Color.clear, Color.Lerp(p.Line, p.Rim, 0.28f), cut, 1.2f);
            Handles.color = WithAlpha(p.Rim, 0.45f);
            Handles.DrawAAPolyLine(1.2f, new Vector3(r.x + c, r.y + 0.6f), new Vector3(r.xMax - 1, r.y + 0.6f));
            Handles.color = prev;
        }

        /// <summary>A hairline across a panel.</summary>
        public static void Rule(Rect r) => Fill(new Rect(r.x, r.y, r.width, 1f), WithAlpha(Current.Line, 0.8f));

        /// <summary>A coloured edge on a row (selection, a status).</summary>
        public static void Stripe(Rect r, Color c, float width = 3f) => Fill(new Rect(r.x, r.y, width, r.height), c);

        // ── The sea ──────────────────────────────────────────────────────────────

        /// <summary>
        /// The ground and the menu's sea lines (SeaLines.shader: a deep wash at the top fading into the ground, a soft
        /// glow, then three layers of lines that drift right to left, far to near, faded at the top and bottom). Lines
        /// rest <c>SeaSpacing</c> apart; each is two sines whose periods divide the loop, so a layer that moves one
        /// loop per period never shows a seam. <paramref name="time"/> in seconds; pass a constant to hold it still.
        /// </summary>
        public static void Sea(Rect r, double time)
        {
            if (Event.current.type != EventType.Repaint) return;
            var p = Current;
            EditorGUI.DrawRect(r, p.Ground);
            // the wash: SeaWash at the top, into the ground by half way
            const int bands = 24;
            for (var i = 0; i < bands; i++)
            {
                var t = i / (float)bands;
                var a = Mathf.Clamp01(1f - t * 2f);
                if (a <= 0f) break;
                EditorGUI.DrawRect(new Rect(r.x, r.y + r.height * t, r.width, r.height / bands + 1f), WithAlpha(p.SeaWash, a * 0.9f));
            }
            // the glow up top
            for (var i = 0; i < 6; i++)
            {
                var w = r.width * (0.7f - i * 0.08f);
                var h = r.height * (0.30f - i * 0.04f);
                EditorGUI.DrawRect(new Rect(r.center.x - w * 0.5f, r.y, w, h), WithAlpha(p.SeaGlow, 0.018f));
            }

            var prev = Handles.color;
            var k1 = 2f * Mathf.PI / SeaLoop;
            var k2 = 3f * k1;
            var rows = Mathf.CeilToInt(r.height / SeaSpacing) + 1;
            const int chunks = 6;
            var step = 8f;
            var pts = new List<Vector3>(64);
            for (var i = 0; i < rows; i++)
            {
                var layer = SeaLayers[((i % 3) + 3) % 3];
                var baseY = r.y + i * SeaSpacing + SeaSpacing * 0.5f;
                var fade = EdgeFade((baseY - r.y) / Mathf.Max(1f, r.height));
                if (fade <= 0.01f) continue;
                var a1 = SeaAmplitude * (1f + 0.45f * (i * 7 % 3));
                var a2 = SeaAmplitude * (0.45f + 0.22f * (i * 5 % 3));
                var shift = (float)(time / layer.Seconds % 1.0) * SeaLoop; // one loop per period, right to left
                var chunkW = r.width / chunks;
                for (var c = 0; c < chunks; c++)
                {
                    var x0 = r.x + c * chunkW;
                    var x1 = x0 + chunkW;
                    pts.Clear();
                    for (var x = x0; x <= x1 + 0.01f; x += step)
                    {
                        var u = (x - r.x) + shift;
                        var y = baseY + a1 * Mathf.Sin(k1 * u + i * 0.9f) + a2 * Mathf.Sin(k2 * u + i * 1.7f);
                        pts.Add(new Vector3(Mathf.Min(x, x1), y));
                    }
                    // across the window: Jade rim, Blue rim, Ruby rim (the shader's cyan, periwinkle, violet)
                    var t = (c + 0.5f) / chunks;
                    var col = t < 0.5f ? Color.Lerp(p.Sea1, p.Sea2, t * 2f) : Color.Lerp(p.Sea2, p.Sea3, (t - 0.5f) * 2f);
                    Handles.color = WithAlpha(col, layer.Alpha * fade);
                    Handles.DrawAAPolyLine(layer.Width * 1.6f, pts.ToArray());
                }
            }
            Handles.color = prev;
        }

        /// <summary>The window's top and bottom fade the lines out (the shader's 14 %).</summary>
        static float EdgeFade(float v) => Mathf.Clamp01(Mathf.Min(v, 1f - v) / 0.14f);

        // ── Widgets ──────────────────────────────────────────────────────────────

        /// <summary>A sliver button in the style guide's meanings. Returns true on click.</summary>
        public static bool Button(Rect r, string label, ButtonKind kind, bool enabled = true, string tooltip = null)
        {
            var p = Current;
            var e = Event.current;
            var hover = enabled && r.Contains(e.mousePosition);
            Color fill, border, text;
            if (!enabled)
            {
                fill = WithAlpha(p.Surface, 0.55f);
                border = WithAlpha(p.Line, 0.7f);
                text = p.TextOff;
            }
            else switch (kind)
            {
                case ButtonKind.Cta:
                    fill = hover ? Color.Lerp(p.CtaFill, Color.white, 0.18f) : p.CtaFill;
                    border = p.CtaFill;
                    text = p.OnFill;
                    break;
                case ButtonKind.Primary:
                    fill = WithAlpha(p.Selection, hover ? 0.26f : 0.12f);
                    border = p.Selection;
                    text = p.Selection;
                    break;
                case ButtonKind.Danger:
                    fill = WithAlpha(p.Danger, hover ? 0.24f : 0.08f);
                    border = p.Danger;
                    text = p.Danger;
                    break;
                default:
                    fill = WithAlpha(p.SurfaceRaised, hover ? 0.55f : 0.30f);
                    border = hover ? p.TextSoft : p.Line;
                    text = hover ? p.Text : p.TextSoft;
                    break;
            }
            Sliver(r, fill, border, SliverSmall);
            var s = ButtonText;
            var prev = s.normal.textColor;
            s.normal.textColor = text;
            GUI.Label(r, new GUIContent(label.ToUpperInvariant(), tooltip), s);
            s.normal.textColor = prev;
            if (enabled) EditorGUIUtility.AddCursorRect(r, MouseCursor.Link);
            if (enabled && e.type == EventType.MouseDown && e.button == 0 && r.Contains(e.mousePosition))
            {
                e.Use();
                return true;
            }
            return false;
        }

        /// <summary>A small status chip: outlined (or filled, for a stop) in one colour.</summary>
        public static void Pill(Rect r, string label, Color c, bool filled = false)
        {
            Sliver(r, filled ? c : WithAlpha(c, 0.14f), c, 5f, 1.2f);
            if (Event.current.type != EventType.Repaint) return;
            var s = PillText;
            var prev = s.normal.textColor;
            s.normal.textColor = filled ? Current.OnFill : c;
            GUI.Label(r, label.ToUpperInvariant(), s);
            s.normal.textColor = prev;
        }

        /// <summary>The width a pill needs for its label.</summary>
        public static float PillWidth(string label) => PillText.CalcSize(new GUIContent(label.ToUpperInvariant())).x + 18f;

        public static float ButtonWidth(string label, float min = 64f)
            => Mathf.Max(min, ButtonText.CalcSize(new GUIContent(label.ToUpperInvariant())).x + 28f);

        /// <summary>A row of tabs; the selected one is underlined in the Jade selection. Returns the selected index.</summary>
        public static int Tabs(Rect r, string[] labels, int selected)
        {
            var p = Current;
            var e = Event.current;
            Fill(new Rect(r.x, r.yMax - 1f, r.width, 1f), WithAlpha(p.Line, 0.9f));
            var x = r.x;
            var s = TabText;
            for (var i = 0; i < labels.Length; i++)
            {
                var w = s.CalcSize(new GUIContent(labels[i].ToUpperInvariant())).x + 36f;
                var tr = new Rect(x, r.y, w, r.height);
                var on = i == selected;
                var hover = tr.Contains(e.mousePosition);
                if (on) Fill(new Rect(tr.x + 8, tr.yMax - 3f, tr.width - 16, 3f), p.Selection);
                if (e.type == EventType.Repaint)
                {
                    var prev = s.normal.textColor;
                    s.normal.textColor = on ? p.Text : hover ? p.Text : p.TextSoft;
                    GUI.Label(tr, labels[i].ToUpperInvariant(), s);
                    s.normal.textColor = prev;
                }
                EditorGUIUtility.AddCursorRect(tr, MouseCursor.Link);
                if (e.type == EventType.MouseDown && e.button == 0 && tr.Contains(e.mousePosition))
                {
                    selected = i;
                    e.Use();
                }
                x += w;
            }
            return selected;
        }

        /// <summary>A check box: a sliver square, filled with the Jade selection when on. Returns the new value.</summary>
        public static bool Check(Rect r, bool on, bool mixed = false)
        {
            var p = Current;
            var e = Event.current;
            var hover = r.Contains(e.mousePosition);
            if (on || mixed) Sliver(r, p.Selection, p.Selection, 4f);
            else Sliver(r, WithAlpha(p.GroundDeep, 0.8f), hover ? p.Selection : p.TextSoft, 4f, 1.2f);
            if (e.type == EventType.Repaint && (on || mixed) && _scrollDepth > 0)
            {
                // Inside a scroll view: the mark is a solid inner square (rects clip; Handles might not).
                EditorGUI.DrawRect(mixed ? new Rect(r.x + 3, r.center.y - 1, r.width - 6, 2) : new Rect(r.x + 4, r.y + 4, r.width - 8, r.height - 8), p.OnFill);
            }
            else if (e.type == EventType.Repaint && (on || mixed))
            {
                var prev = Handles.color;
                Handles.color = p.OnFill;
                if (mixed)
                    Handles.DrawAAPolyLine(2.2f, new Vector3(r.x + 4, r.center.y), new Vector3(r.xMax - 4, r.center.y));
                else
                    Handles.DrawAAPolyLine(2.2f, new Vector3(r.x + 3.5f, r.center.y), new Vector3(r.x + r.width * 0.42f, r.yMax - 4f),
                                           new Vector3(r.xMax - 3.5f, r.y + 4f));
                Handles.color = prev;
            }
            EditorGUIUtility.AddCursorRect(r, MouseCursor.Link);
            if (e.type == EventType.MouseDown && e.button == 0 && r.Contains(e.mousePosition))
            {
                e.Use();
                return !on;
            }
            return on;
        }

        /// <summary>A thin bar that runs while something works: a segment drifting like the sea.</summary>
        public static void Working(Rect r, double time)
        {
            if (Event.current.type != EventType.Repaint) return;
            var p = Current;
            EditorGUI.DrawRect(r, WithAlpha(p.Line, 0.5f));
            var w = r.width * 0.25f;
            var x = r.x + (float)(time * 0.6 % 1.0) * (r.width + w) - w;
            var seg = Rect.MinMaxRect(Mathf.Max(r.x, x), r.y, Mathf.Min(r.xMax, x + w), r.yMax);
            if (seg.width > 0) EditorGUI.DrawRect(seg, p.Selection);
        }
    }
}
