using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using CosmicShore.Editor.Froglet;
using UnityEditor;
using UnityEngine;
using Rgba = CosmicShore.Editor.Studios.StudioPreviewCanvas.Rgba;

namespace CosmicShore.Editor.Studios
{
    /// <summary>
    /// The Vessel Studio HOME: the web hub's front page (<c>Docs/Studios/VesselStudio/index.html</c>) in Unity, read
    /// from the same files, so a studio added to <c>studios.json</c> shows up here with nothing else to change. The
    /// colours are the studio's (<c>studio-theme.js</c>: the Stoat's night tokens and amber chrome; card accents from
    /// <c>studio-domains.js</c>), not the editor skin's: this page is a studio page.
    /// </summary>
    public sealed partial class VesselStudioWindow
    {
        const string StudioDir = "Docs/Studios/VesselStudio";
        const float PreviewHeight = 150f, CardMinWidth = 300f, Gap = 14f, Pad = 16f;
        const int PreviewPixelsW = 320, PreviewPixelsH = 150;   // the web canvas is 640×300 at half scale
        const double FrameSeconds = 1.0 / 30.0;

        /// <summary>studios.json, as JsonUtility reads it (fields the page does not use are skipped).</summary>
        [Serializable]
        public sealed class Catalog
        {
            public string web, mirror, hub;
            public StudioEntry[] studios;
        }

        [Serializable]
        public sealed class StudioEntry
        {
            public string id, name, file, kind, summary, accent, docs, engineMode, engineNote;
        }

        // The studio look (studio-theme.js), night only: the stage and the hub are night whatever the editor skin.
        static class Look
        {
            public static readonly Color Bg = C("#0b1026"), Panel = C("#121a3a"), Line = C("#27336a");
            public static readonly Color Fg = C("#e8ecff"), Dim = C("#9aa6d6"), Accent = C("#ffb347"), Off = C("#5d6488");
            public static readonly Color OnAccent = C("#1a1200"), Bad = C("#ff7a8a");

            public static Color C(string hex) => ToColor(Rgba.Hex(hex));
            public static Color ToColor(Rgba c) => new(c.R, c.G, c.B, c.A);

            static GUIStyle _eyebrow, _h1, _h2, _lede, _name, _body, _chip, _button, _note;
            public static GUIStyle Eyebrow => _eyebrow ??= Make(11, FontStyle.Bold, Dim);
            public static GUIStyle H1 => _h1 ??= Make(40, FontStyle.Bold, Fg);
            public static GUIStyle H2 => _h2 ??= Make(17, FontStyle.Bold, Fg);
            public static GUIStyle Lede => _lede ??= Make(13, FontStyle.Normal, Dim, wrap: true);
            public static GUIStyle Name => _name ??= Make(26, FontStyle.Bold, Fg);
            public static GUIStyle Body => _body ??= Make(12, FontStyle.Normal, Dim, wrap: true);
            public static GUIStyle Chip => _chip ??= Make(10, FontStyle.Normal, Fg, TextAnchor.MiddleCenter);
            public static GUIStyle Button => _button ??= Make(11, FontStyle.Bold, Fg, TextAnchor.MiddleCenter);
            public static GUIStyle Note => _note ??= Make(11, FontStyle.Normal, Dim, wrap: true);

            static GUIStyle Make(int size, FontStyle style, Color color, TextAnchor anchor = TextAnchor.UpperLeft, bool wrap = false) =>
                new(EditorStyles.label)
                {
                    fontSize = size, fontStyle = style, alignment = anchor, wordWrap = wrap, richText = false,
                    padding = new RectOffset(0, 0, 0, 0), margin = new RectOffset(0, 0, 0, 0),
                    normal = { textColor = color }, hover = { textColor = color },
                };

            static readonly Dictionary<(GUIStyle, Color), GUIStyle> Tints = new();

            /// <summary>Text in a colour other than the style's (made once per style and colour, not per frame).</summary>
            public static GUIStyle Tinted(GUIStyle s, Color c)
            {
                if (!Tints.TryGetValue((s, c), out var t))
                    Tints[(s, c)] = t = new GUIStyle(s) { normal = { textColor = c }, hover = { textColor = c } };
                return t;
            }
        }

        Catalog _catalog;
        string _homeError;
        Dictionary<string, Color> _domains = new();
        string[] _fleet = Array.Empty<string>();
        readonly Dictionary<string, Texture2D> _previewTex = new();
        readonly Dictionary<string, StudioPreviewCanvas> _previewCanvas = new();
        Vector2 _homeScroll;
        float _homeHeight = 900f;
        double _lastFrame;

        static string Root => Path.GetDirectoryName(Application.dataPath) ?? ".";
        static string StudioPath(string file) => Path.Combine(Root, StudioDir, file);

        void LoadHome()
        {
            _homeError = null;
            try
            {
                _catalog = JsonUtility.FromJson<Catalog>(File.ReadAllText(StudioPath("studios.json")));
                _domains = ParseDomains(File.ReadAllText(StudioPath("studio-domains.js")));
                string hub = StudioPath("index.html");
                _fleet = File.Exists(hub) ? ParseFleet(File.ReadAllText(hub)) : Array.Empty<string>();
            }
            catch (Exception e)
            {
                _catalog = null;
                _homeError = $"Could not read the Vessel Studio from {StudioDir}: {e.Message}. Pull Ys-bleeding-edge.";
            }
        }

        /// <summary>The game's domain colours from the generated <c>studio-domains.js</c>, by key. Pure, tested.</summary>
        public static Dictionary<string, Color> ParseDomains(string js)
        {
            var result = new Dictionary<string, Color>();
            foreach (Match m in Regex.Matches(js, @"key:\s*'(\w+)'[^}]*?color:\s*'(#[0-9a-fA-F]{6})'"))
                result[m.Groups[1].Value] = Look.C(m.Groups[2].Value);
            return result;
        }

        /// <summary>The hub's "no studio yet" fleet list (<c>&lt;div class="fleet"&gt;</c>), in order. Pure, tested.</summary>
        public static string[] ParseFleet(string html)
        {
            var block = Regex.Match(html, @"<div class=""fleet""[^>]*>(.*?)</div>", RegexOptions.Singleline);
            if (!block.Success) return Array.Empty<string>();
            var names = new List<string>();
            foreach (Match m in Regex.Matches(block.Groups[1].Value, @"<span>([^<]+)</span>")) names.Add(m.Groups[1].Value.Trim());
            return names.ToArray();
        }

        /// <summary>A studio's card colour: a domain key (studio-domains.js), a #hex, or the studio amber.</summary>
        Color AccentOf(StudioEntry s)
        {
            if (string.IsNullOrEmpty(s.accent)) return Look.Accent;
            if (_domains.TryGetValue(s.accent, out var d)) return d;
            try { return Look.C(s.accent); }
            catch (FormatException) { return Look.Accent; }
        }

        string StudioName(string id)
        {
            if (_catalog?.studios != null)
                foreach (var s in _catalog.studios)
                    if (s.id == id) return s.name;
            return id ?? "";
        }

        void OpenStudio(string id)
        {
            if (_catalog?.studios != null)
                foreach (var s in _catalog.studios)
                    if (s.id == id) { OpenPage(s.file); return; }
        }

        static void OpenPage(string file)
        {
            string path = StudioPath(file);
            if (!File.Exists(path))
            {
                EditorUtility.DisplayDialog("Vessel Studio", $"{StudioDir}/{file} is not in this checkout. Pull Ys-bleeding-edge.", "OK");
                return;
            }
            LaunchPrisma.OpenStudio(file);   // the build of this checkout, served by Amoebius (/vessel-studio D33)
        }

        /// <summary>Keeps the previews moving: a repaint at most 30 times a second, and only on the home page.</summary>
        void Animate()
        {
            if (!string.IsNullOrEmpty(_view)) return;
            double now = EditorApplication.timeSinceStartup;
            if (now - _lastFrame < FrameSeconds) return;
            _lastFrame = now;
            Repaint();
        }

        // ------------------------------------------------------------------ the page

        void DrawHome()
        {
            var full = new Rect(0f, 0f, position.width, position.height);
            FrogletEditorPalette.DrawRect(full, Look.Bg);

            float viewW = position.width - 14f;   // the scrollbar's lane is always kept, so the layout never flips between widths
            _homeScroll = GUI.BeginScrollView(full, _homeScroll, new Rect(0f, 0f, viewW, _homeHeight), false, false, GUIStyle.none, GUI.skin.verticalScrollbar);
            float cw = Mathf.Min(1120f, viewW - 2f * Pad), x = (viewW - cw) * 0.5f, y = 26f;

            // ---- header: eyebrow, VESSEL STUDIO, the lede, the page's own links ----
            GUI.Label(new Rect(x, y, cw, 14f), "COSMIC SHORE · FROGLET", Look.Eyebrow);
            y += 20f;
            var vessel = new GUIContent("VESSEL ");
            float vw = Look.H1.CalcSize(vessel).x;
            GUI.Label(new Rect(x, y, vw, 46f), vessel, Look.H1);
            GUI.Label(new Rect(x + vw, y, cw - vw, 46f), "STUDIO", Look.Tinted(Look.H1, Look.Accent));
            y += 52f;
            const string lede = "Pick a vessel and its studio opens: fly it with gamepad, keys or your phone's thumbs, switch its " +
                                "play-style types and element levels, and read what every number does. The same pages open here, " +
                                "in Amoebius, in the claude.ai artifact and in your phone's browser.";
            float lh = Look.Lede.CalcHeight(new GUIContent(lede), Mathf.Min(cw, 640f));
            GUI.Label(new Rect(x, y, Mathf.Min(cw, 640f), lh), lede, Look.Lede);
            y += lh + 12f;

            float bx = x;
            if (Button(ref bx, y, "OPEN THE HUB", Look.Accent, true, "The web hub itself, built from this checkout and served by Amoebius, in its own window"))
                OpenPage(_catalog?.hub ?? "index.html");
            if (Button(ref bx, y, "CLAUDE.AI ARTIFACT", Look.Accent, false, "The one Vessel Studio artifact: Ask, shared requests, the decision log and Sync live there"))
                Application.OpenURL(_catalog?.web ?? StudioUrl);
            if (Button(ref bx, y, "AMOEBIUS", Look.Accent, false, "Amoebius's VESSEL STUDIO page: PLAY IN ENGINE flies the game's own vessel"))
                EditorApplication.delayCall += LaunchPrisma.OpenAmoebiusStudiosPage;
            y += 26f + 26f;

            if (_homeError != null)
            {
                GUI.Label(new Rect(x, y, cw, 40f), _homeError, Look.Tinted(Look.Body, Look.Bad));
                y += 48f;
            }

            // ---- the studios ----
            GUI.Label(new Rect(x, y, cw, 22f), "STUDIOS", Look.H2);
            y += 30f;
            var studios = _catalog?.studios ?? Array.Empty<StudioEntry>();
            int cols = Mathf.Max(1, Mathf.FloorToInt((cw + Gap) / (CardMinWidth + Gap)));
            float cardW = (cw - Gap * (cols - 1)) / cols;
            for (int i = 0; i < studios.Length; i += cols)
            {
                float rowH = 0f;
                for (int c = 0; c < cols && i + c < studios.Length; c++) rowH = Mathf.Max(rowH, CardHeight(studios[i + c], cardW));
                for (int c = 0; c < cols && i + c < studios.Length; c++)
                    DrawCard(new Rect(x + c * (cardW + Gap), y, cardW, rowH), studios[i + c]);
                y += rowH + Gap;
            }

            // ---- the fleet without a studio ----
            if (_fleet.Length > 0)
            {
                float fx = x;
                foreach (var v in _fleet)
                {
                    float w = Look.Chip.CalcSize(new GUIContent(v)).x + 18f;
                    if (fx + w > x + cw) { fx = x; y += 30f; }
                    var r = new Rect(fx, y, w, 24f);
                    FrogletEditorPalette.DrawCard(r, Color.clear, Look.Line);
                    GUI.Label(r, v, Look.Tinted(Look.Chip, Look.Off));
                    fx += w + 6f;
                }
                y += 32f;
                const string none = "No studio yet. Ask the studio agent (in the artifact, or Amoebius's AGENT) to start one; each needs the vessel's own numbers read from its assets first.";
                float nh = Look.Note.CalcHeight(new GUIContent(none), cw);
                GUI.Label(new Rect(x, y, cw, nh), none, Look.Note);
                y += nh + 18f;
            }

            const string foot = "A studio opens in its own window: the artifact's own pages from this checkout, so it looks and plays exactly as on claude.ai " +
                                "(Unity has no web view, so it is an Edge or Chrome app window). Ask, shared requests, the decision log and Sync need claude.ai; " +
                                "each page links there. Tune in Unity puts a studio's six tabs over the vessel's real assets, live while you play.";
            float fh = Look.Note.CalcHeight(new GUIContent(foot), cw);
            GUI.Label(new Rect(x, y, cw, fh), foot, Look.Note);
            y += fh + 28f;

            GUI.EndScrollView();
            if (Event.current.type == EventType.Repaint && !Mathf.Approximately(_homeHeight, y)) { _homeHeight = y; Repaint(); }
        }

        float CardHeight(StudioEntry s, float w) =>
            Pad + PreviewHeight + 12f + 32f + 26f + Look.Body.CalcHeight(new GUIContent(s.summary ?? ""), w - 2f * Pad) + 14f + 26f + Pad;

        void DrawCard(Rect r, StudioEntry s)
        {
            var accent = AccentOf(s);
            bool hover = r.Contains(Event.current.mousePosition);
            FrogletEditorPalette.DrawCard(r, Look.Panel, hover ? accent : Look.Line);
            float x = r.x + Pad, w = r.width - 2f * Pad, y = r.y + Pad;

            var preview = new Rect(x, y, w, PreviewHeight);
            if (Event.current.type == EventType.Repaint)
                GUI.DrawTextureWithTexCoords(preview, Preview(s, accent), new Rect(0f, 1f, 1f, -1f));   // the canvas is top-down
            y += PreviewHeight + 12f;

            GUI.Label(new Rect(x, y, w, 30f), (s.name ?? s.id).ToUpperInvariant(), Look.Tinted(Look.Name, accent));
            y += 32f;
            string kind = s.kind ?? "";
            float kw = Mathf.Min(w, Look.Chip.CalcSize(new GUIContent(kind)).x + 16f);
            var chip = new Rect(x, y, kw, 20f);
            FrogletEditorPalette.DrawCard(chip, accent.WithAlpha(0.08f), accent);
            GUI.Label(chip, kind, Look.Tinted(Look.Chip, accent));
            y += 26f;
            float sh = Look.Body.CalcHeight(new GUIContent(s.summary ?? ""), w);
            GUI.Label(new Rect(x, y, w, sh), s.summary ?? "", Look.Body);

            // the actions sit on the card's floor, so cards in a row line up
            float by = r.yMax - Pad - 26f, bx = x;
            if (Button(ref bx, by, "OPEN STUDIO ▸", accent, true, $"Open the {s.name} studio (built from this checkout and served by Amoebius: Sync, Ask and Decisions work) in its own window"))
                OpenPage(s.file);
            if (Tuners.ContainsKey(s.id) &&
                Button(ref bx, by, "TUNE IN UNITY", accent, false, $"The {s.name} studio's six tabs over the vessel's real assets, live while you play"))
            {
                _view = s.id;
                GUIUtility.ExitGUI();
            }
            if (!string.IsNullOrEmpty(s.engineMode) &&
                Button(ref bx, by, "PLAY IN ENGINE", accent, false, (s.engineNote ?? "") + " Opens Amoebius's VESSEL STUDIO page: pick " + s.name + ", then PLAY IN ENGINE."))
                EditorApplication.delayCall += LaunchPrisma.OpenAmoebiusStudiosPage;

            // the whole card opens the studio, as on the web hub (drawn after the buttons, so they keep their clicks)
            if (GUI.Button(r, GUIContent.none, GUIStyle.none)) OpenPage(s.file);
            EditorGUIUtility.AddCursorRect(r, MouseCursor.Link);
        }

        /// <summary>This frame of a studio's preview, drawn on the CPU (well under a millisecond) and uploaded.</summary>
        Texture2D Preview(StudioEntry s, Color accent)
        {
            if (!_previewTex.TryGetValue(s.id, out var tex) || !tex)
            {
                tex = new Texture2D(PreviewPixelsW, PreviewPixelsH, TextureFormat.RGBA32, false)
                {
                    hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp,
                };
                _previewTex[s.id] = tex;
                _previewCanvas[s.id] = new StudioPreviewCanvas(PreviewPixelsW, PreviewPixelsH, 0.5f);
            }
            var canvas = _previewCanvas[s.id];
            Rgba Domain(string key) => _domains.TryGetValue(key, out var c) ? new Rgba(c.r, c.g, c.b) : new Rgba(accent.r, accent.g, accent.b);
            StudioPreviews.Draw(s.id, canvas, (float)EditorApplication.timeSinceStartup, Domain, new Rgba(accent.r, accent.g, accent.b));
            tex.SetPixelData(canvas.Pixels, 0);
            tex.Apply(false);
            return tex;
        }

        /// <summary>A studio button (the page's own, not the editor skin's): filled when primary, outlined otherwise. Advances <paramref name="x"/>.</summary>
        static bool Button(ref float x, float y, string label, Color accent, bool primary, string tooltip)
        {
            float w = Look.Button.CalcSize(new GUIContent(label)).x + 24f;
            var r = new Rect(x, y, w, 26f);
            x += w + 8f;
            bool hover = r.Contains(Event.current.mousePosition);
            if (primary) FrogletEditorPalette.DrawCard(r, hover ? Color.Lerp(accent, Color.white, 0.15f) : accent, accent);
            else FrogletEditorPalette.DrawCard(r, accent.WithAlpha(hover ? 0.18f : 0.06f), hover ? accent : Look.Line);
            GUI.Label(r, label, Look.Tinted(Look.Button, primary ? Look.OnAccent : Look.Fg));
            EditorGUIUtility.AddCursorRect(r, MouseCursor.Link);
            return GUI.Button(r, new GUIContent("", tooltip), GUIStyle.none);
        }
    }
}
