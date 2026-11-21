using System;
using System.IO;
using System.Numerics;
using ImGuiNET;
using Silk.NET.OpenGL;
using StbImageSharp;

namespace CosmicShore.Launcher
{
    /// <summary>The launcher's backdrop and accent theme (SETTINGS > LOOK).</summary>
    public sealed partial class LauncherApp
    {
        static readonly string[] Backgrounds = { "SYNTHWAVE", "WARP", "STARFIELD", "NEBULA", "AURORA", "LATTICE", "SOLID", "IMAGE" };

        float _bgTime;
        int _appliedTheme = -1;
        string? _bgImagePath;
        uint _bgImage;
        Vector2 _bgImageSize;
        string _bgImageError = "";

        void DrawBackground(Vector2 size, float dt)
        {
            if (_appliedTheme != _s.Theme) { Neon.ApplyTheme(_s.Theme); _appliedTheme = _s.Theme; }
            var dl = ImGui.GetBackgroundDrawList();
            float m = Math.Clamp(_s.BackgroundMotion, 0f, 2f);
            float mdt = dt * m;
            _bgTime += mdt;
            float t = _bgTime;
            dl.AddRectFilledMultiColor(Vector2.Zero, size, Neon.U(Neon.Space1), Neon.U(Neon.Space1), Neon.U(Neon.Space0), Neon.U(Neon.Space0));
            switch (_s.Background)
            {
                case 1: DrawSynthwave(dl, size, mdt, t, nebula: true, warp: true, grid: false); break;
                case 2: DrawSynthwave(dl, size, mdt, t, nebula: false, warp: false, grid: false); break;
                case 3: DrawNebula(dl, size, t); break;
                case 4: DrawAurora(dl, size, t); break;
                case 5: DrawLattice(dl, size, t); break;
                case 6: break; // the gradient alone
                case 7: if (!DrawImage(dl, size)) DrawSynthwave(dl, size, mdt, t, true, true, true); break;
                default: DrawSynthwave(dl, size, mdt, t, nebula: true, warp: true, grid: true); break;
            }
            float dim = Math.Clamp(_s.BackgroundDim, 0f, 0.9f);
            if (dim > 0) dl.AddRectFilled(Vector2.Zero, size, Neon.U(Neon.Space0, dim));
        }

        /// <summary>Slow, layered gas clouds in the theme's colours, with a still star field.</summary>
        void DrawNebula(ImDrawListPtr dl, Vector2 size, float t)
        {
            void Cloud(float cx, float cy, float r, Vector4 col, float a, float phase)
            {
                var c = new Vector2(size.X * (cx + 0.05f * MathF.Sin(t * 0.04f + phase)), size.Y * (cy + 0.04f * MathF.Cos(t * 0.03f + phase)));
                for (int i = 14; i >= 1; i--) dl.AddCircleFilled(c, size.X * r * i / 14f, Neon.U(col, a * 0.045f), 64);
            }
            Cloud(0.25f, 0.35f, 0.42f, Neon.Violet, 0.8f, 0f);
            Cloud(0.75f, 0.60f, 0.38f, Neon.Magenta, 0.6f, 1.7f);
            Cloud(0.55f, 0.20f, 0.25f, Neon.Cyan, 0.45f, 3.1f);
            Cloud(0.10f, 0.85f, 0.22f, Neon.Cyan, 0.30f, 4.4f);
            var rng = new Random(11);
            for (int i = 0; i < 220; i++)
            {
                var p = new Vector2((float)rng.NextDouble() * size.X, (float)rng.NextDouble() * size.Y);
                float tw = 0.45f + 0.35f * MathF.Sin(t * (0.6f + (float)rng.NextDouble()) + i);
                dl.AddCircleFilled(p, 0.6f + (float)rng.NextDouble() * 1.1f, Neon.U(Neon.Ink, tw));
            }
        }

        /// <summary>Curtains of light: stacked sine ribbons that drift and breathe.</summary>
        void DrawAurora(ImDrawListPtr dl, Vector2 size, float t)
        {
            DrawSynthwave(dl, size, 0, t, nebula: false, warp: false, grid: false);
            Vector4[] cols = { Neon.Cyan, Neon.Violet, Neon.Magenta };
            for (int band = 0; band < 3; band++)
            {
                float baseY = size.Y * (0.28f + band * 0.12f);
                int steps = 90;
                for (int i = 0; i < steps; i++)
                {
                    float x0 = size.X * i / steps, x1 = size.X * (i + 1) / steps;
                    float Y(float x) => baseY + size.Y * 0.06f * MathF.Sin(x / size.X * 6.0f + t * (0.25f + band * 0.07f) + band * 2f)
                                               + size.Y * 0.03f * MathF.Sin(x / size.X * 13.0f - t * 0.4f + band);
                    float h = size.Y * (0.10f + 0.05f * MathF.Sin(x0 / size.X * 4f + t * 0.3f + band));
                    float a = 0.18f + 0.08f * MathF.Sin(x0 / size.X * 9f + t * 0.5f + band * 1.3f);
                    dl.AddRectFilledMultiColor(new Vector2(x0, Y(x0) - h), new Vector2(x1 + 1, Y(x0)),
                        Neon.U(cols[band], 0), Neon.U(cols[band], 0), Neon.U(cols[band], a), Neon.U(cols[band], a));
                }
            }
        }

        /// <summary>The HyperSea's prism lattice: a hex field whose cells pulse in slow waves.</summary>
        void DrawLattice(ImDrawListPtr dl, Vector2 size, float t)
        {
            float r = 34f, w = r * MathF.Sqrt(3f);
            var centre = size * 0.5f;
            for (int row = -1; row * r * 1.5f < size.Y + r; row++)
                for (int col = -1; col * w < size.X + w; col++)
                {
                    var c = new Vector2(col * w + (row & 1) * w * 0.5f, row * r * 1.5f);
                    float d = (c - centre).Length() / size.X;
                    float wave = 0.5f + 0.5f * MathF.Sin(d * 14f - t * 1.2f);
                    float pulse = MathF.Pow(wave, 6f);
                    var col4 = Neon.Mix(Neon.Violet, Neon.Cyan, pulse);
                    for (int k = 0; k < 6; k++)
                    {
                        float a0 = MathF.PI / 6 + k * MathF.PI / 3, a1 = a0 + MathF.PI / 3;
                        dl.AddLine(c + new Vector2(MathF.Cos(a0), MathF.Sin(a0)) * r * 0.92f, c + new Vector2(MathF.Cos(a1), MathF.Sin(a1)) * r * 0.92f,
                            Neon.U(col4, 0.06f + 0.30f * pulse), 1f + pulse);
                    }
                    if (pulse > 0.6f) dl.AddCircleFilled(c, r * 0.5f, Neon.U(Neon.Cyan, 0.05f * pulse), 6);
                }
        }

        /// <summary>The user's own picture, scaled to cover the window. False when it cannot be shown.</summary>
        unsafe bool DrawImage(ImDrawListPtr dl, Vector2 size)
        {
            var path = _s.BackgroundImage?.Trim().Trim('"') ?? "";
            if (path != _bgImagePath)
            {
                _bgImagePath = path;
                if (_bgImage != 0) { _gl.DeleteTexture(_bgImage); _bgImage = 0; }
                _bgImageError = "";
                if (path.Length > 0)
                {
                    try
                    {
                        var img = ImageResult.FromMemory(File.ReadAllBytes(path), ColorComponents.RedGreenBlueAlpha);
                        _bgImage = _gl.GenTexture();
                        _gl.BindTexture(TextureTarget.Texture2D, _bgImage);
                        fixed (byte* p = img.Data)
                            _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba, (uint)img.Width, (uint)img.Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, p);
                        _gl.GenerateMipmap(TextureTarget.Texture2D);
                        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
                        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
                        _bgImageSize = new Vector2(img.Width, img.Height);
                    }
                    catch (Exception e) { _bgImageError = e is FileNotFoundException or DirectoryNotFoundException ? "file not found" : "not a PNG/JPG/BMP/TGA image"; }
                }
            }
            if (_bgImage == 0) return false;
            float s = MathF.Max(size.X / _bgImageSize.X, size.Y / _bgImageSize.Y);
            var drawn = _bgImageSize * s;
            var a = (size - drawn) * 0.5f;
            dl.AddImage((IntPtr)_bgImage, a, a + drawn);
            return true;
        }

        void DrawLookSettings()
        {
            Row("Background", () =>
            {
                int bg = Math.Clamp(_s.Background, 0, Backgrounds.Length - 1);
                ImGui.PushItemWidth(360);
                if (ImGui.Combo("##bg", ref bg, Backgrounds, Backgrounds.Length)) { _s.Background = bg; _dirty = true; }
                ImGui.PopItemWidth();
            });
            if (_s.Background == 7)
                Row("Image", () =>
                {
                    Text("##bgimg", @"C:\Pictures\wallpaper.png", () => _s.BackgroundImage, v => _s.BackgroundImage = v);
                    if (_bgImageError.Length > 0) { ImGui.PushFont(Neon.Small); ImGui.TextColored(Neon.Red, _bgImageError); ImGui.PopFont(); }
                });
            Row("Theme", () => Segmented("theme", Neon.Themes, Math.Clamp(_s.Theme, 0, Neon.Themes.Length - 1), i => _s.Theme = i, Neon.Cyan));
            Row("Motion", () =>
            {
                float m = _s.BackgroundMotion;
                ImGui.PushItemWidth(360);
                if (ImGui.SliderFloat("##motion", ref m, 0f, 2f, m < 0.01f ? "still" : "%.1fx")) { _s.BackgroundMotion = m; _dirty = true; }
                ImGui.PopItemWidth();
            });
            Row("", () => Toggle("Animations", () => _s.Animations, v => _s.Animations = v, "Intro splash and page transitions"));
            Row("Dim", () =>
            {
                float d = _s.BackgroundDim;
                ImGui.PushItemWidth(360);
                if (ImGui.SliderFloat("##dim", ref d, 0f, 0.9f, "%.2f")) { _s.BackgroundDim = d; _dirty = true; }
                ImGui.PopItemWidth();
            });
        }
    }
}
