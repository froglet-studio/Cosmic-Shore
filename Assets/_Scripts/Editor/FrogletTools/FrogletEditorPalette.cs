using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Editor.Froglet
{
    /// <summary>
    /// Shared look for every Froglet editor window: one palette, one set of button drawers.
    /// Every Froglet tool window should draw through this rather than hand-rolling colours,
    /// so the toolchain reads as one product instead of a pile of separate windows.
    ///
    /// Colours are tuned for the dark Editor skin (the default) and lightened automatically for
    /// the light skin, so a button is legible either way.
    /// </summary>
    public static class FrogletEditorPalette
    {
        // ── Accents ──────────────────────────────────────────────────────────────
        public static readonly Color Jade = new(0.16f, 0.78f, 0.56f);
        public static readonly Color Ruby = new(0.93f, 0.27f, 0.38f);
        public static readonly Color Gold = new(0.98f, 0.75f, 0.18f);
        public static readonly Color Azure = new(0.24f, 0.60f, 0.95f);
        public static readonly Color Violet = new(0.62f, 0.42f, 0.94f);
        public static readonly Color Cyan = new(0.16f, 0.78f, 0.82f);
        public static readonly Color Coral = new(0.98f, 0.51f, 0.31f);
        public static readonly Color Magenta = new(0.90f, 0.36f, 0.71f);
        public static readonly Color Slate = new(0.48f, 0.55f, 0.64f);
        public static readonly Color Lime = new(0.62f, 0.83f, 0.24f);
        public static readonly Color Indigo = new(0.42f, 0.47f, 0.96f);

        // ── Semantics ────────────────────────────────────────────────────────────
        public static readonly Color Ok = Jade;
        public static readonly Color Warn = Gold;
        public static readonly Color Error = Ruby;
        public static readonly Color Info = Azure;
        public static readonly Color Muted = new(0.55f, 0.57f, 0.62f);

        static readonly Dictionary<FrogletToolCategory, Color> CategoryColors = new()
        {
            { FrogletToolCategory.Build, Coral },
            { FrogletToolCategory.SceneSetup, Azure },
            { FrogletToolCategory.GameModes, Jade },
            { FrogletToolCategory.Vessels, Violet },
            { FrogletToolCategory.Ecology, Lime },
            { FrogletToolCategory.Performance, Gold },
            { FrogletToolCategory.Interface, Cyan },
            { FrogletToolCategory.Services, Magenta },
            { FrogletToolCategory.Utilities, Slate },
            { FrogletToolCategory.Diagnostics, Indigo },
            { FrogletToolCategory.Qa, Cyan },
            { FrogletToolCategory.Ai, Ruby },
            { FrogletToolCategory.Ftue, Violet },
        };

        public static Color ColorFor(FrogletToolCategory c)
            => CategoryColors.TryGetValue(c, out var col) ? col : Slate;

        public static string LabelFor(FrogletToolCategory c) => c switch
        {
            FrogletToolCategory.Build => "Build & Release",
            FrogletToolCategory.SceneSetup => "Scenes",
            FrogletToolCategory.GameModes => "Game Modes",
            FrogletToolCategory.Vessels => "Vessels & HUD",
            FrogletToolCategory.Ecology => "Prisms & Ecology",
            FrogletToolCategory.Performance => "Performance",
            FrogletToolCategory.Interface => "UI",
            FrogletToolCategory.Services => "Services & Data",
            FrogletToolCategory.Diagnostics => "Diagnostics & Health",
            FrogletToolCategory.Qa => "QA",
            FrogletToolCategory.Ai => "AI",
            FrogletToolCategory.Ftue => "FTUE",
            FrogletToolCategory.Utilities => "Utilities",
            _ => "Utilities",
        };

        // ── Skin awareness ───────────────────────────────────────────────────────

        public static bool IsDark => EditorGUIUtility.isProSkin;

        /// <summary>Panel background, a touch off the editor's own grey so cards read as cards.</summary>
        public static Color Surface => IsDark ? new Color(0.19f, 0.20f, 0.22f) : new Color(0.84f, 0.85f, 0.87f);

        public static Color SurfaceRaised => IsDark ? new Color(0.24f, 0.25f, 0.28f) : new Color(0.92f, 0.93f, 0.95f);

        public static Color HeaderText => IsDark ? new Color(0.93f, 0.94f, 0.96f) : new Color(0.12f, 0.13f, 0.15f);

        public static Color BodyText => IsDark ? new Color(0.76f, 0.78f, 0.82f) : new Color(0.25f, 0.27f, 0.30f);

        /// <summary>
        /// Adapts an accent for the current skin: dark skin keeps it vivid, light skin deepens it
        /// so white text on top still passes.
        /// </summary>
        public static Color Adapt(Color c) => IsDark ? c : c * 0.82f;

        public static Color WithAlpha(this Color c, float a) => new(c.r, c.g, c.b, a);

        // ── Primitives ───────────────────────────────────────────────────────────

        static Texture2D _pixel;

        static Texture2D Pixel
        {
            get
            {
                if (_pixel != null) return _pixel;
                _pixel = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
                _pixel.SetPixel(0, 0, Color.white);
                _pixel.Apply();
                return _pixel;
            }
        }

        public static void DrawRect(Rect r, Color color)
        {
            if (Event.current.type != EventType.Repaint) return;
            var prev = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(r, Pixel);
            GUI.color = prev;
        }

        /// <summary>Filled rect plus a 1px border of the same hue at higher alpha.</summary>
        public static void DrawCard(Rect r, Color fill, Color border, float borderWidth = 1f)
        {
            DrawRect(r, fill);
            DrawRect(new Rect(r.x, r.y, r.width, borderWidth), border);
            DrawRect(new Rect(r.x, r.yMax - borderWidth, r.width, borderWidth), border);
            DrawRect(new Rect(r.x, r.y, borderWidth, r.height), border);
            DrawRect(new Rect(r.xMax - borderWidth, r.y, borderWidth, r.height), border);
        }

        /// <summary>Left accent stripe used to colour-code rows without shouting.</summary>
        public static void DrawAccentStripe(Rect row, Color accent, float width = 3f)
            => DrawRect(new Rect(row.x, row.y, width, row.height), accent);

        // ── Styles ───────────────────────────────────────────────────────────────

        static GUIStyle _title, _subtitle, _section, _cardTitle, _cardBody, _cardBodyWrapped, _pill, _sectionHeader;

        public static GUIStyle Title => _title ??= new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 17,
            alignment = TextAnchor.MiddleLeft,
            normal = { textColor = HeaderText },
        };

        public static GUIStyle Subtitle => _subtitle ??= new GUIStyle(EditorStyles.label)
        {
            fontSize = 11,
            wordWrap = true,
            normal = { textColor = Muted },
        };

        public static GUIStyle SectionHeader => _sectionHeader ??= new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 12,
            normal = { textColor = HeaderText },
        };

        public static GUIStyle SectionLabel => _section ??= new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 11,
            alignment = TextAnchor.MiddleLeft,
        };

        public static GUIStyle CardTitle => _cardTitle ??= new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 12,
            alignment = TextAnchor.MiddleLeft,
            normal = { textColor = HeaderText },
        };

        public static GUIStyle CardBody => _cardBody ??= new GUIStyle(EditorStyles.label)
        {
            fontSize = 10,
            wordWrap = false,
            alignment = TextAnchor.MiddleLeft,
            normal = { textColor = Muted },
        };

        /// <summary>Multi-line flavour of <see cref="CardBody"/> for message/stack excerpts.</summary>
        public static GUIStyle CardBodyWrapped => _cardBodyWrapped ??= new GUIStyle(CardBody)
        {
            wordWrap = true,
        };

        public static GUIStyle Pill => _pill ??= new GUIStyle(EditorStyles.miniLabel)
        {
            fontSize = 9,
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold,
        };

        // ── Buttons ──────────────────────────────────────────────────────────────

        /// <summary>
        /// A solid, colour-filled button. Returns true on click. Uses a tinted background rect plus
        /// a transparent GUI.Button on top so the colour survives every editor skin.
        /// </summary>
        public static bool ColorButton(Rect r, string label, Color accent, string tooltip = null,
                                       bool enabled = true, bool outline = false)
        {
            var a = Adapt(accent);
            bool hover = r.Contains(Event.current.mousePosition);

            Color fill, border, text;
            if (!enabled)
            {
                fill = Surface;
                border = Muted.WithAlpha(0.35f);
                text = Muted;
            }
            else if (outline)
            {
                fill = a.WithAlpha(hover ? 0.28f : 0.12f);
                border = a.WithAlpha(0.9f);
                text = IsDark ? a : a * 0.75f;
            }
            else
            {
                fill = hover ? Color.Lerp(a, Color.white, 0.18f) : a;
                border = Color.Lerp(a, Color.black, 0.25f);
                text = Color.white;
            }

            DrawCard(r, fill, border);

            var style = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11,
                normal = { textColor = text },
            };
            GUI.Label(r, label, style);

            using (new EditorGUI.DisabledScope(!enabled))
            {
                var content = string.IsNullOrEmpty(tooltip) ? GUIContent.none : new GUIContent("", tooltip);
                if (GUI.Button(r, content, GUIStyle.none))
                    return enabled;
            }

            if (hover) EditorGUIUtility.AddCursorRect(r, MouseCursor.Link);
            return false;
        }

        /// <summary>Layout-flow flavour of <see cref="ColorButton(Rect,string,Color,string,bool,bool)"/>.</summary>
        public static bool ColorButton(string label, Color accent, float width, float height = 24f,
                                       string tooltip = null, bool enabled = true, bool outline = false)
        {
            var r = GUILayoutUtility.GetRect(width, height, GUILayout.Width(width), GUILayout.Height(height));
            return ColorButton(r, label, accent, tooltip, enabled, outline);
        }

        /// <summary>Small rounded status pill, e.g. "OK" / "3 ISSUES".</summary>
        public static void StatusPill(Rect r, string label, Color accent)
        {
            var a = Adapt(accent);
            DrawCard(r, a.WithAlpha(0.20f), a.WithAlpha(0.85f));
            var s = new GUIStyle(Pill) { normal = { textColor = IsDark ? a : a * 0.7f } };
            GUI.Label(r, label, s);
        }

        /// <summary>Full-width banner used as a window header.</summary>
        public static void Banner(string title, string subtitle, Color accent, float height = 52f)
        {
            var r = GUILayoutUtility.GetRect(0, height, GUILayout.ExpandWidth(true));
            var a = Adapt(accent);
            DrawRect(r, SurfaceRaised);
            DrawRect(new Rect(r.x, r.y, 4f, r.height), a);
            DrawRect(new Rect(r.x, r.yMax - 1f, r.width, 1f), a.WithAlpha(0.5f));

            var titleRect = new Rect(r.x + 14f, r.y + 7f, r.width - 20f, 22f);
            GUI.Label(titleRect, title, Title);

            if (!string.IsNullOrEmpty(subtitle))
            {
                var subRect = new Rect(r.x + 14f, r.y + 27f, r.width - 20f, height - 30f);
                GUI.Label(subRect, subtitle, Subtitle);
            }
        }

        public static void HorizontalRule(float pad = 6f)
        {
            GUILayout.Space(pad);
            var r = GUILayoutUtility.GetRect(0, 1, GUILayout.ExpandWidth(true));
            DrawRect(r, Muted.WithAlpha(0.25f));
            GUILayout.Space(pad);
        }

        // ── The Froglet colour families (Docs/UI_STYLE_GUIDE.md §1) ──────────────

        /// <summary>
        /// The style guide's families, each with its one meaning, from <c>Docs/UI/froglet-tokens.json</c>
        /// (the Deep Sea theme on the dark skin, its light twin Shore on the light one). A window
        /// that shows go / no-go or a commitment draws through these, not through
        /// <see cref="Ok"/> / <see cref="Error"/>, whose hues predate the guide (Design feedback
        /// queue #19, <c>Docs/UI_REDESIGN_TASKS.md</c>).
        /// </summary>
        public static class Families
        {
            /// <summary>Lime: go, and the call to action you can still change.</summary>
            public static Color Cta => IsDark ? Rgb(0x99FF80) : Rgb(0x487C1F);
            /// <summary>Lime as a button fill (both skins), with <see cref="OnFill"/> text.</summary>
            public static Color CtaFill => Rgb(0x99FF80);
            /// <summary>Text on a Lime fill.</summary>
            public static Color OnFill => Rgb(0x00041F);
            /// <summary>Fire: danger, no-go; dashed it is "at risk" (there is no amber family).</summary>
            public static Color Danger => IsDark ? Rgb(0xFF4B3A) : Rgb(0xCD373A);
            /// <summary>Ruby: no way back (promote, roll back, Play, Ready).</summary>
            public static Color Commit => IsDark ? Rgb(0xA600FF) : Rgb(0x893BFF);
            /// <summary>Text on a Ruby fill.</summary>
            public static Color OnCommit => Color.white;
            /// <summary>Jade: the selection and the focus ring.</summary>
            public static Color Selection => IsDark ? Rgb(0x00D4FF) : Rgb(0x007D73);

            static Color Rgb(int rgb) =>
                new(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f);
        }

        // ── The corner sliver (Docs/UI_STYLE_GUIDE.md §4) ────────────────────────

        static readonly Vector3[] _sliver = new Vector3[6];
        static readonly Vector3[] _sliverLoop = new Vector3[7];

        /// <summary>
        /// A card with the guide's corner sliver: the top-left and bottom-right corners cut by
        /// <paramref name="cut"/> (14 on large surfaces, 10 on buttons and chips). A dashed border
        /// is drawn <paramref name="borderWidth"/> lines deep. Repaint only, like <see cref="DrawRect"/>.
        /// </summary>
        public static void DrawSliver(Rect r, Color fill, Color border, float cut = 10f,
                                      float borderWidth = 1f, bool dashed = false)
        {
            if (Event.current.type != EventType.Repaint || r.width <= 0f || r.height <= 0f) return;
            var prev = Handles.color;
            SliverPoints(r, cut, _sliver);
            if (fill.a > 0f)
            {
                Handles.color = fill;
                Handles.DrawAAConvexPolygon(_sliver);
            }
            if (border.a > 0f && borderWidth > 0f)
            {
                Handles.color = border;
                if (dashed)
                {
                    for (int line = 0; line < Mathf.Max(1, Mathf.RoundToInt(borderWidth)); line++)
                    {
                        SliverPoints(new Rect(r.x + line, r.y + line, r.width - 2 * line, r.height - 2 * line),
                            Mathf.Max(0f, cut - line * 0.4f), _sliver);
                        for (int i = 0; i < _sliver.Length; i++)
                            Handles.DrawDottedLine(_sliver[i], _sliver[(i + 1) % _sliver.Length], 4f);
                    }
                }
                else
                {
                    float inset = borderWidth * 0.5f;
                    SliverPoints(new Rect(r.x + inset, r.y + inset, r.width - borderWidth, r.height - borderWidth), cut, _sliver);
                    for (int i = 0; i < _sliver.Length; i++) _sliverLoop[i] = _sliver[i];
                    _sliverLoop[_sliver.Length] = _sliver[0];
                    Handles.DrawAAPolyLine(borderWidth * 1.5f, _sliverLoop);
                }
            }
            Handles.color = prev;
        }

        static void SliverPoints(Rect r, float cut, Vector3[] into)
        {
            cut = Mathf.Max(0f, Mathf.Min(cut, r.width * 0.5f, r.height * 0.5f));
            into[0] = new Vector3(r.x + cut, r.y);
            into[1] = new Vector3(r.xMax, r.y);
            into[2] = new Vector3(r.xMax, r.yMax - cut);
            into[3] = new Vector3(r.xMax - cut, r.yMax);
            into[4] = new Vector3(r.x, r.yMax);
            into[5] = new Vector3(r.x, r.y + cut);
        }

        static GUIStyle _sliverLabel;

        /// <summary>
        /// A button with the corner sliver, in the colours given (a family's fill and its text).
        /// Returns true on click. Disabled it draws grey, as every disabled button does.
        /// </summary>
        public static bool SliverButton(Rect r, string label, Color fill, Color border, Color text,
                                        string tooltip = null, bool enabled = true)
        {
            bool hover = enabled && r.Contains(Event.current.mousePosition);
            if (!enabled)
            {
                fill = Surface;
                border = Muted.WithAlpha(0.35f);
                text = Muted;
            }
            else if (hover)
                fill = Color.Lerp(fill, Color.white, fill.a < 0.5f ? 0.10f : 0.15f);

            DrawSliver(r, fill, border, 6f);
            _sliverLabel ??= new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11,
                clipping = TextClipping.Clip,
                richText = false,
            };
            _sliverLabel.normal.textColor = text;
            GUI.Label(r, label, _sliverLabel);

            using (new EditorGUI.DisabledScope(!enabled))
            {
                var content = string.IsNullOrEmpty(tooltip) ? GUIContent.none : new GUIContent("", tooltip);
                if (GUI.Button(r, content, GUIStyle.none))
                    return enabled;
            }
            if (hover) EditorGUIUtility.AddCursorRect(r, MouseCursor.Link);
            return false;
        }

        /// <summary>A sliver button in a family: Lime (call to action), Ruby (no way back) or plain.</summary>
        public static bool FamilyButton(Rect r, string label, bool cta, bool commit, string tooltip = null, bool enabled = true)
        {
            if (cta) return SliverButton(r, label, Families.CtaFill, Families.CtaFill, Families.OnFill, tooltip, enabled);
            if (commit) return SliverButton(r, label, Families.Commit.WithAlpha(0.24f), Families.Commit, HeaderText, tooltip, enabled);
            return SliverButton(r, label, SurfaceRaised, Muted.WithAlpha(0.6f), HeaderText, tooltip, enabled);
        }
    }
}
