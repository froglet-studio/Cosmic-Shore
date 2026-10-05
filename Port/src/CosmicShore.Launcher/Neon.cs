using System;
using System.Numerics;
using ImGuiNET;

namespace CosmicShore.Launcher
{
    /// <summary>
    /// The launcher's look: Cosmic Shore's palette (cyan core, magenta and violet accents, deep
    /// navy space) drawn as chamfered neon panels and buttons with layered glow. Everything is
    /// immediate-mode drawing on ImGui's draw lists - no textures besides the logo art.
    /// </summary>
    public static class Neon
    {
        // The accent and space colours are a theme (SETTINGS > LOOK); everything reads them live.
        public static Vector4 Cyan = new(0.20f, 0.92f, 1.00f, 1f);
        public static Vector4 Magenta = new(1.00f, 0.22f, 0.86f, 1f);
        public static Vector4 Violet = new(0.55f, 0.36f, 1.00f, 1f);
        public static readonly Vector4 Lime = new(0.62f, 1.00f, 0.30f, 1f);
        public static readonly Vector4 Amber = new(1.00f, 0.72f, 0.18f, 1f);
        public static readonly Vector4 Red = new(1.00f, 0.30f, 0.38f, 1f);
        public static readonly Vector4 Ink = new(0.86f, 0.93f, 1.00f, 1f);
        public static readonly Vector4 Dim = new(0.52f, 0.60f, 0.78f, 1f);
        public static Vector4 Space0 = new(0.012f, 0.010f, 0.045f, 1f);
        public static Vector4 Space1 = new(0.045f, 0.030f, 0.140f, 1f);

        /// <summary>Accent themes: the game's own look, its four domain colours, and a quiet grey.</summary>
        public static readonly string[] Themes = { "COSMIC", "JADE", "RUBY", "GOLD", "ICE", "MONO" };

        public static void ApplyTheme(int theme)
        {
            (Cyan, Magenta, Violet, Space0, Space1) = theme switch
            {
                1 => (V(0.25f, 1.00f, 0.70f), V(0.10f, 0.75f, 0.55f), V(0.20f, 0.55f, 0.65f), V(0.005f, 0.030f, 0.030f), V(0.020f, 0.090f, 0.080f)),
                2 => (V(1.00f, 0.42f, 0.52f), V(0.95f, 0.15f, 0.35f), V(0.65f, 0.15f, 0.45f), V(0.035f, 0.008f, 0.020f), V(0.120f, 0.020f, 0.060f)),
                3 => (V(1.00f, 0.82f, 0.30f), V(1.00f, 0.52f, 0.15f), V(0.85f, 0.40f, 0.25f), V(0.030f, 0.020f, 0.008f), V(0.110f, 0.070f, 0.020f)),
                4 => (V(0.70f, 0.90f, 1.00f), V(0.35f, 0.55f, 1.00f), V(0.45f, 0.50f, 0.95f), V(0.010f, 0.020f, 0.045f), V(0.030f, 0.070f, 0.150f)),
                5 => (V(0.92f, 0.94f, 0.98f), V(0.62f, 0.66f, 0.74f), V(0.45f, 0.48f, 0.56f), V(0.015f, 0.016f, 0.020f), V(0.060f, 0.064f, 0.075f)),
                _ => (V(0.20f, 0.92f, 1.00f), V(1.00f, 0.22f, 0.86f), V(0.55f, 0.36f, 1.00f), V(0.012f, 0.010f, 0.045f), V(0.045f, 0.030f, 0.140f)),
            };
        }

        static Vector4 V(float r, float g, float b) => new(r, g, b, 1f);
        public static readonly Vector4 Panel = new(0.035f, 0.045f, 0.120f, 0.82f);

        public static ImFontPtr Body, Small, Heading, Hero, Mono, Title;

        public static uint U(Vector4 c, float alpha = 1f) =>
            ImGui.ColorConvertFloat4ToU32(new Vector4(c.X, c.Y, c.Z, c.W * Math.Clamp(alpha, 0f, 1f)));

        public static Vector4 Mix(Vector4 a, Vector4 b, float t) => Vector4.Lerp(a, b, Math.Clamp(t, 0f, 1f));

        public static float Time => (float)ImGui.GetTime();

        /// <summary>A rectangle with its top-left and bottom-right corners cut - the game's UI shape.</summary>
        public static void ChamferPath(ImDrawListPtr dl, Vector2 a, Vector2 b, float cut)
        {
            cut = Math.Min(cut, Math.Min(b.X - a.X, b.Y - a.Y) * 0.45f);
            dl.PathLineTo(new Vector2(a.X + cut, a.Y));
            dl.PathLineTo(new Vector2(b.X, a.Y));
            dl.PathLineTo(new Vector2(b.X, b.Y - cut));
            dl.PathLineTo(new Vector2(b.X - cut, b.Y));
            dl.PathLineTo(new Vector2(a.X, b.Y));
            dl.PathLineTo(new Vector2(a.X, a.Y + cut));
        }

        public static void ChamferFill(ImDrawListPtr dl, Vector2 a, Vector2 b, float cut, uint col)
        {
            ChamferPath(dl, a, b, cut);
            dl.PathFillConvex(col);
        }

        /// <summary>A chamfered outline with a soft glow built from wider, fainter strokes.</summary>
        public static void ChamferGlow(ImDrawListPtr dl, Vector2 a, Vector2 b, float cut, Vector4 color, float strength = 1f, float width = 1.6f)
        {
            for (int i = 4; i >= 1; i--)
            {
                float grow = i * 1.6f;
                ChamferPath(dl, a - new Vector2(grow), b + new Vector2(grow), cut + grow * 0.6f);
                dl.PathStroke(U(color, 0.07f * strength * (5 - i)), ImDrawFlags.Closed, 2.2f);
            }
            ChamferPath(dl, a, b, cut);
            dl.PathStroke(U(color, 0.95f * Math.Min(1f, strength)), ImDrawFlags.Closed, width);
        }

        /// <summary>A glass panel: dark translucent body, faint inner gradient, neon rim, corner ticks.</summary>
        public static void PanelFrame(ImDrawListPtr dl, Vector2 a, Vector2 b, Vector4 accent, string? label = null, float strength = 0.7f)
        {
            ChamferFill(dl, a, b, 14, U(Panel));
            dl.AddRectFilledMultiColor(a + new Vector2(2, 2), new Vector2(b.X - 2, a.Y + 60),
                U(accent, 0.10f), U(accent, 0.04f), U(accent, 0f), U(accent, 0f));
            ChamferGlow(dl, a, b, 14, accent, strength, 1.2f);
            // corner ticks
            dl.AddLine(new Vector2(b.X - 46, a.Y + 6), new Vector2(b.X - 8, a.Y + 6), U(accent, 0.9f), 2f);
            dl.AddLine(new Vector2(a.X + 8, b.Y - 6), new Vector2(a.X + 46, b.Y - 6), U(accent, 0.9f), 2f);
            if (label != null)
            {
                ImGui.PushFont(Small);
                var p = new Vector2(a.X + 22, a.Y + 10);
                dl.AddRectFilled(p + new Vector2(-8, 3), p + new Vector2(-4, 13), U(accent));
                dl.AddText(p, U(accent), label);
                ImGui.PopFont();
            }
        }

        /// <summary>Text drawn with a coloured halo underneath.</summary>
        public static void GlowText(ImDrawListPtr dl, ImFontPtr font, float size, Vector2 pos, Vector4 color, string text, float glow = 1f)
        {
            for (int i = 0; i < 8; i++)
            {
                float ang = i * MathF.PI / 4f;
                var off = new Vector2(MathF.Cos(ang), MathF.Sin(ang)) * 2.2f;
                dl.AddText(font, size, pos + off, U(color, 0.16f * glow), text);
            }
            dl.AddText(font, size, pos, U(Mix(color, Ink, 0.55f)), text);
        }

        /// <summary>
        /// The launcher's button: chamfered, gradient-filled, with a rim glow that brightens on
        /// hover, a sheen that sweeps across it, and an optional play triangle. Returns clicked.
        /// </summary>
        public static bool Button(string id, string label, Vector2 size, Vector4 color, ImFontPtr font, float fontSize,
            bool enabled = true, bool hero = false, bool playIcon = false, string? sub = null)
        {
            var dl = ImGui.GetWindowDrawList();
            var a = ImGui.GetCursorScreenPos();
            var b = a + size;
            ImGui.InvisibleButton(id, size);
            bool hover = enabled && ImGui.IsItemHovered();
            bool held = enabled && ImGui.IsItemActive();
            bool clicked = enabled && ImGui.IsItemClicked();
            if (hover) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);

            var c = enabled ? color : Mix(color, Dim, 0.75f);
            float pulse = hero && enabled ? 0.5f + 0.5f * MathF.Sin(Time * 2.6f) : 0f;
            float cut = Math.Min(size.Y * 0.32f, 22f);
            float lift = held ? 1f : 0f;
            a.Y += lift; b.Y += lift;

            // body: dark core with a vertical gradient of the accent colour
            ChamferFill(dl, a, b, cut, U(Space0, 0.92f));
            float fillA = enabled ? (hover ? 0.42f : 0.26f) + pulse * 0.10f : 0.08f;
            dl.PushClipRect(a, b, true);
            dl.AddRectFilledMultiColor(a, b, U(c, fillA * 0.55f), U(c, fillA * 0.30f), U(c, fillA), U(c, fillA * 1.15f));
            // scan stripes
            for (float y = a.Y + 3; y < b.Y; y += 4) dl.AddLine(new Vector2(a.X, y), new Vector2(b.X, y), U(Space0, 0.18f), 1f);
            // sheen sweep
            if (enabled)
            {
                float period = hero ? 2.8f : 4.5f;
                float t = (Time % period) / period;
                float x = a.X - size.X * 0.4f + t * size.X * 1.8f;
                for (int i = 0; i < 18; i++)
                {
                    float w = i * 1.6f;
                    dl.AddQuadFilled(new Vector2(x + w, a.Y), new Vector2(x + w + 2, a.Y), new Vector2(x + w + 2 - size.Y * 0.6f, b.Y), new Vector2(x + w - size.Y * 0.6f, b.Y),
                        U(Ink, 0.05f * (1f - MathF.Abs(i - 9) / 9f) * (hover ? 1.6f : 1f)));
                }
            }
            dl.PopClipRect();
            ChamferGlow(dl, a, b, cut, c, enabled ? (hover ? 1.6f : 1.0f) + pulse * 0.8f : 0.25f, hero ? 2.4f : 1.6f);

            // label (+ optional play triangle and sub-caption)
            ImGui.PushFont(font);
            var ts = ImGui.CalcTextSize(label) * (fontSize / font.FontSize);
            float iconW = playIcon ? fontSize * 0.8f + 16 : 0;
            float total = ts.X + iconW;
            float subH = sub != null ? 18 : 0;
            var tp = new Vector2(a.X + (size.X - total) * 0.5f + iconW, a.Y + (size.Y - ts.Y - subH) * 0.5f);
            if (playIcon)
            {
                float h = fontSize * 0.78f;
                var p0 = new Vector2(tp.X - iconW, tp.Y + (ts.Y - h) * 0.5f);
                dl.AddTriangleFilled(p0, p0 + new Vector2(h * 0.9f, h * 0.5f), p0 + new Vector2(0, h), U(Mix(c, Ink, 0.6f)));
                dl.AddTriangle(p0, p0 + new Vector2(h * 0.9f, h * 0.5f), p0 + new Vector2(0, h), U(c, 0.5f), 4f);
            }
            if (enabled) GlowText(dl, font, fontSize, tp, c, label, hover ? 1.4f : 0.9f);
            else dl.AddText(font, fontSize, tp, U(Dim, 0.6f), label);
            ImGui.PopFont();
            if (sub != null)
            {
                ImGui.PushFont(Small);
                var ss = ImGui.CalcTextSize(sub);
                dl.AddText(new Vector2(a.X + (size.X - ss.X) * 0.5f, tp.Y + ts.Y + 4), U(enabled ? Mix(c, Ink, 0.3f) : Dim, enabled ? 0.85f : 0.5f), sub);
                ImGui.PopFont();
            }
            return clicked;
        }

        /// <summary>A small status pill: dot + text, green when ok, amber/red otherwise.</summary>
        public static void Pill(ImDrawListPtr dl, Vector2 pos, string text, Vector4 color, out float width)
        {
            ImGui.PushFont(Small);
            var ts = ImGui.CalcTextSize(text);
            width = ts.X + 30;
            var a = pos; var b = pos + new Vector2(width, 24);
            ChamferFill(dl, a, b, 7, U(color, 0.12f));
            ChamferPath(dl, a, b, 7);
            dl.PathStroke(U(color, 0.75f), ImDrawFlags.Closed, 1f);
            float blink = 0.6f + 0.4f * MathF.Sin(Time * 3f + pos.X * 0.05f);
            dl.AddCircleFilled(a + new Vector2(12, 12), 4.5f, U(color, blink));
            dl.AddCircle(a + new Vector2(12, 12), 7f, U(color, 0.25f * blink), 16, 2f);
            dl.AddText(a + new Vector2(22, (24 - ts.Y) * 0.5f), U(Mix(color, Ink, 0.45f)), text);
            ImGui.PopFont();
        }

        /// <summary>A segmented progress bar; negative progress animates an indeterminate scanner.</summary>
        public static void Progress(ImDrawListPtr dl, Vector2 a, Vector2 b, float progress, Vector4 color)
        {
            ChamferFill(dl, a, b, 6, U(Space0, 0.9f));
            ChamferPath(dl, a, b, 6);
            dl.PathStroke(U(color, 0.5f), ImDrawFlags.Closed, 1f);
            int segs = Math.Max(8, (int)((b.X - a.X) / 12));
            float segW = (b.X - a.X - 8) / segs;
            for (int i = 0; i < segs; i++)
            {
                float x0 = a.X + 4 + i * segW;
                float on;
                if (progress >= 0) on = Math.Clamp(progress * segs - i, 0f, 1f);
                else
                {
                    float head = (Time * 0.6f % 1f) * (segs + 10) - 5;
                    on = Math.Clamp(1f - Math.Abs(i - head) / 5f, 0f, 1f);
                }
                dl.AddRectFilled(new Vector2(x0 + 1, a.Y + 4), new Vector2(x0 + segW - 2, b.Y - 4), U(color, 0.10f + 0.85f * on));
            }
        }

        public static void Tooltip(string text)
        {
            if (!ImGui.IsItemHovered(ImGuiHoveredFlags.DelayShort)) return;
            ImGui.PushFont(Small);
            ImGui.SetTooltip(text);
            ImGui.PopFont();
        }

        /// <summary>Global ImGui style: dark glass, cyan accents, square-ish edges.</summary>
        public static void ApplyStyle()
        {
            var s = ImGui.GetStyle();
            s.WindowRounding = 0; s.FrameRounding = 3; s.PopupRounding = 3; s.GrabRounding = 2; s.TabRounding = 2;
            s.ScrollbarRounding = 2; s.ChildRounding = 4;
            s.WindowBorderSize = 0; s.FrameBorderSize = 1; s.PopupBorderSize = 1;
            s.FramePadding = new Vector2(10, 7); s.ItemSpacing = new Vector2(10, 9); s.ScrollbarSize = 10;
            s.WindowPadding = new Vector2(0, 0);
            var c = s.Colors;
            c[(int)ImGuiCol.Text] = Ink;
            c[(int)ImGuiCol.TextDisabled] = new Vector4(0.34f, 0.38f, 0.52f, 1f);
            c[(int)ImGuiCol.WindowBg] = new Vector4(0, 0, 0, 0);
            c[(int)ImGuiCol.ChildBg] = new Vector4(0, 0, 0, 0);
            c[(int)ImGuiCol.PopupBg] = new Vector4(0.03f, 0.03f, 0.10f, 0.97f);
            c[(int)ImGuiCol.Border] = new Vector4(0.20f, 0.92f, 1f, 0.35f);
            c[(int)ImGuiCol.FrameBg] = new Vector4(0.02f, 0.03f, 0.09f, 0.9f);
            c[(int)ImGuiCol.FrameBgHovered] = new Vector4(0.06f, 0.10f, 0.22f, 0.95f);
            c[(int)ImGuiCol.FrameBgActive] = new Vector4(0.08f, 0.14f, 0.30f, 1f);
            c[(int)ImGuiCol.CheckMark] = Cyan;
            c[(int)ImGuiCol.SliderGrab] = Cyan;
            c[(int)ImGuiCol.SliderGrabActive] = Magenta;
            c[(int)ImGuiCol.Button] = new Vector4(0.08f, 0.16f, 0.30f, 0.9f);
            c[(int)ImGuiCol.ButtonHovered] = new Vector4(0.12f, 0.36f, 0.55f, 1f);
            c[(int)ImGuiCol.ButtonActive] = new Vector4(0.55f, 0.15f, 0.55f, 1f);
            c[(int)ImGuiCol.Header] = new Vector4(0.10f, 0.30f, 0.50f, 0.7f);
            c[(int)ImGuiCol.HeaderHovered] = new Vector4(0.15f, 0.45f, 0.70f, 0.8f);
            c[(int)ImGuiCol.HeaderActive] = new Vector4(0.50f, 0.16f, 0.55f, 0.9f);
            c[(int)ImGuiCol.ScrollbarBg] = new Vector4(0, 0, 0, 0.2f);
            c[(int)ImGuiCol.ScrollbarGrab] = new Vector4(0.20f, 0.92f, 1f, 0.35f);
            c[(int)ImGuiCol.ScrollbarGrabHovered] = new Vector4(0.20f, 0.92f, 1f, 0.6f);
            c[(int)ImGuiCol.ScrollbarGrabActive] = Magenta;
            c[(int)ImGuiCol.Separator] = new Vector4(0.20f, 0.92f, 1f, 0.25f);
            c[(int)ImGuiCol.TextSelectedBg] = new Vector4(1f, 0.22f, 0.86f, 0.35f);
        }

        // ---------------------------------------------------------------- icons (vector, 24px box centred on c)

        public static void IconPlay(ImDrawListPtr dl, Vector2 c, uint col) =>
            dl.AddTriangleFilled(c + new Vector2(-7, -10), c + new Vector2(10, 0), c + new Vector2(-7, 10), col);

        public static void IconPhone(ImDrawListPtr dl, Vector2 c, uint col)
        {
            dl.AddRect(c + new Vector2(-8, -12), c + new Vector2(8, 12), col, 3f, ImDrawFlags.None, 2f);
            dl.AddLine(c + new Vector2(-3, 8), c + new Vector2(3, 8), col, 2f);
        }

        public static void IconGear(ImDrawListPtr dl, Vector2 c, uint col)
        {
            for (int i = 0; i < 8; i++)
            {
                float a = i * MathF.PI / 4f;
                var d = new Vector2(MathF.Cos(a), MathF.Sin(a));
                dl.AddLine(c + d * 8, c + d * 12, col, 3f);
            }
            dl.AddCircle(c, 8, col, 20, 2f);
            dl.AddCircle(c, 3, col, 12, 2f);
        }

        public static void IconTerminal(ImDrawListPtr dl, Vector2 c, uint col)
        {
            dl.AddRect(c + new Vector2(-12, -10), c + new Vector2(12, 10), col, 2f, ImDrawFlags.None, 2f);
            dl.AddLine(c + new Vector2(-7, -4), c + new Vector2(-2, 0), col, 2f);
            dl.AddLine(c + new Vector2(-2, 0), c + new Vector2(-7, 4), col, 2f);
            dl.AddLine(c + new Vector2(1, 5), c + new Vector2(7, 5), col, 2f);
        }

        public static void IconSliders(ImDrawListPtr dl, Vector2 c, uint col)
        {
            for (int i = -1; i <= 1; i++)
            {
                float y = c.Y + i * 7;
                dl.AddLine(new Vector2(c.X - 11, y), new Vector2(c.X + 11, y), col, 2f);
                float k = c.X + (i == 0 ? 5 : i < 0 ? -5 : 1);
                dl.AddCircleFilled(new Vector2(k, y), 3.5f, col);
            }
        }

        public static void IconChat(ImDrawListPtr dl, Vector2 c, uint col)
        {
            dl.AddRect(c + new Vector2(-12, -10), c + new Vector2(12, 6), col, 4f, ImDrawFlags.None, 2f);
            dl.AddTriangleFilled(c + new Vector2(-6, 6), c + new Vector2(0, 6), c + new Vector2(-8, 12), col);
        }

        public static void IconRefresh(ImDrawListPtr dl, Vector2 c, uint col)
        {
            dl.PathArcTo(c, 8, 0.6f, MathF.PI * 1.85f, 18);
            dl.PathStroke(col, ImDrawFlags.None, 2f);
            var tip = c + new Vector2(MathF.Cos(0.6f), MathF.Sin(0.6f)) * 8;
            dl.AddTriangleFilled(tip + new Vector2(-4, -1), tip + new Vector2(4, -3), tip + new Vector2(1, 5), col);
        }

        public static void IconDownload(ImDrawListPtr dl, Vector2 c, uint col)
        {
            dl.AddLine(c + new Vector2(0, -10), c + new Vector2(0, 4), col, 2f);
            dl.AddTriangleFilled(c + new Vector2(-6, 0), c + new Vector2(6, 0), c + new Vector2(0, 7), col);
            dl.AddLine(c + new Vector2(-10, 10), c + new Vector2(10, 10), col, 2f);
        }

        public static void IconFolder(ImDrawListPtr dl, Vector2 c, uint col)
        {
            dl.AddRect(c + new Vector2(-11, -6), c + new Vector2(11, 9), col, 2f, ImDrawFlags.None, 2f);
            dl.AddLine(c + new Vector2(-11, -6), c + new Vector2(-9, -10), col, 2f);
            dl.AddLine(c + new Vector2(-9, -10), c + new Vector2(-2, -10), col, 2f);
            dl.AddLine(c + new Vector2(-2, -10), c + new Vector2(0, -6), col, 2f);
        }

        public static void IconUp(ImDrawListPtr dl, Vector2 c, uint col)
        { dl.AddLine(c + new Vector2(-6, 3), c + new Vector2(0, -4), col, 2f); dl.AddLine(c + new Vector2(0, -4), c + new Vector2(6, 3), col, 2f); }

        public static void IconDown(ImDrawListPtr dl, Vector2 c, uint col)
        { dl.AddLine(c + new Vector2(-6, -3), c + new Vector2(0, 4), col, 2f); dl.AddLine(c + new Vector2(0, 4), c + new Vector2(6, -3), col, 2f); }

        public static void Chevron(ImDrawListPtr dl, Vector2 c, bool open, uint col)
        {
            if (open) { dl.AddLine(c + new Vector2(-5, -2), c + new Vector2(0, 3), col, 2f); dl.AddLine(c + new Vector2(0, 3), c + new Vector2(5, -2), col, 2f); }
            else { dl.AddLine(c + new Vector2(-2, -5), c + new Vector2(3, 0), col, 2f); dl.AddLine(c + new Vector2(3, 0), c + new Vector2(-2, 5), col, 2f); }
        }

        /// <summary>A square ghost button holding one icon.</summary>
        public static bool IconButton(string id, Action<ImDrawListPtr, Vector2, uint> icon, float size, bool enabled = true)
        {
            var dl = ImGui.GetWindowDrawList();
            var a = ImGui.GetCursorScreenPos();
            var b = a + new Vector2(size);
            bool clicked = ImGui.InvisibleButton(id, new Vector2(size)) && enabled;
            bool hov = enabled && ImGui.IsItemHovered();
            if (hov) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            ChamferFill(dl, a, b, 6, U(hov ? Cyan : Space0, hov ? 0.18f : 0.7f));
            ChamferPath(dl, a, b, 6);
            dl.PathStroke(U(enabled ? Cyan : Dim, hov ? 0.9f : 0.35f), ImDrawFlags.Closed, 1.1f);
            icon(dl, (a + b) * 0.5f, U(enabled ? (hov ? Ink : Cyan) : Dim, enabled ? 1f : 0.5f));
            return clicked;
        }
    }
}
