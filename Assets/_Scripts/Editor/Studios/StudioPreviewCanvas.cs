using System;
using System.Globalization;

namespace CosmicShore.Editor.Studios
{
    /// <summary>
    /// A tiny software canvas for the Vessel Studio home's card previews: the web hub draws each studio's looping
    /// flight on a 640×300 2D canvas (<c>Docs/Studios/VesselStudio/index.html</c>, "bay previews"), and this draws the
    /// same calls into an RGBA buffer the window uploads to a texture. Callers use the web page's coordinates; the
    /// canvas scales them to its own pixel size. Rows are stored top-down, like the web canvas.
    ///
    /// <para>Plain C#, no UnityEngine, so a preview can be rendered and checked outside the editor.</para>
    /// </summary>
    public sealed class StudioPreviewCanvas
    {
        /// <summary>A straight-alpha colour, each channel 0..1.</summary>
        public readonly struct Rgba
        {
            public readonly float R, G, B, A;
            public Rgba(float r, float g, float b, float a = 1f) { R = r; G = g; B = b; A = a; }
            public Rgba WithAlpha(float a) => new(R, G, B, a);

            /// <summary>Parses <c>#rgb</c>, <c>#rrggbb</c> or <c>#rrggbbaa</c>, as the web page writes them.</summary>
            public static Rgba Hex(string hex)
            {
                string h = hex.TrimStart('#');
                if (h.Length == 3) h = string.Concat(h[0], h[0], h[1], h[1], h[2], h[2]);
                if (h.Length != 6 && h.Length != 8) throw new FormatException($"Not a colour: {hex}");
                float C(int i) => int.Parse(h.Substring(i, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255f;
                return new Rgba(C(0), C(2), C(4), h.Length == 8 ? C(6) : 1f);
            }

            static Rgba Lerp(Rgba a, Rgba b, float t) =>
                new(a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t, a.B + (b.B - a.B) * t, a.A + (b.A - a.A) * t);

            /// <summary>The colour at <paramref name="t"/> along gradient stops (offsets ascending, 0..1).</summary>
            public static Rgba Along((float At, Rgba Color)[] stops, float t)
            {
                if (t <= stops[0].At) return stops[0].Color;
                for (int i = 1; i < stops.Length; i++)
                    if (t <= stops[i].At)
                    {
                        float span = stops[i].At - stops[i - 1].At;
                        return Lerp(stops[i - 1].Color, stops[i].Color, span <= 0f ? 1f : (t - stops[i - 1].At) / span);
                    }
                return stops[^1].Color;
            }
        }

        public readonly int Width, Height;
        /// <summary>Pixels per web-canvas unit.</summary>
        public readonly float Scale;
        /// <summary>RGBA32, top row first.</summary>
        public readonly byte[] Pixels;

        /// <summary>The web canvas's size in its own units (what the draw calls are written against).</summary>
        public float W => Width / Scale;
        public float H => Height / Scale;

        public StudioPreviewCanvas(int width, int height, float scale)
        {
            Width = width; Height = height; Scale = scale;
            Pixels = new byte[width * height * 4];
        }

        public void Clear(Rgba c)
        {
            byte r = B(c.R), g = B(c.G), b = B(c.B);
            for (int i = 0; i < Pixels.Length; i += 4) { Pixels[i] = r; Pixels[i + 1] = g; Pixels[i + 2] = b; Pixels[i + 3] = 255; }
        }

        /// <summary><c>fillRect</c>, with partial coverage at the edges.</summary>
        public void FillRect(float x, float y, float w, float h, Rgba c)
        {
            float x0 = x * Scale, y0 = y * Scale, x1 = (x + w) * Scale, y1 = (y + h) * Scale;
            for (int py = Math.Max(0, (int)MathF.Floor(y0)); py < Math.Min(Height, (int)MathF.Ceiling(y1)); py++)
            {
                float cy = Math.Clamp(MathF.Min(y1, py + 1) - MathF.Max(y0, py), 0f, 1f);
                for (int px = Math.Max(0, (int)MathF.Floor(x0)); px < Math.Min(Width, (int)MathF.Ceiling(x1)); px++)
                    Blend(px, py, c, cy * Math.Clamp(MathF.Min(x1, px + 1) - MathF.Max(x0, px), 0f, 1f));
            }
        }

        /// <summary>A filled <c>arc(x, y, r, 0, 2π)</c>.</summary>
        public void FillCircle(float cx, float cy, float r, Rgba c) =>
            Shade(cx, cy, r, (px, py, d) => (c, Math.Clamp(r * Scale - d + 0.5f, 0f, 1f)));

        /// <summary>A disc of radius <paramref name="r"/> filled with <c>createRadialGradient(x, y, r0, x, y, r1)</c>.</summary>
        public void FillRadial(float cx, float cy, float r, float r0, float r1, (float At, Rgba Color)[] stops) =>
            Shade(cx, cy, r, (px, py, d) =>
            {
                float t = Math.Clamp((d / Scale - r0) / Math.Max(1e-4f, r1 - r0), 0f, 1f);
                return (Rgba.Along(stops, t), Math.Clamp(r * Scale - d + 0.5f, 0f, 1f));
            });

        /// <summary>A stroked ellipse (<c>ellipse</c> + <c>stroke</c>), line width in web units (the canvas default is 1).</summary>
        public void StrokeEllipse(float cx, float cy, float rx, float ry, Rgba c, float lineWidth = 1f)
        {
            float ax = rx * Scale, ay = ry * Scale, half = MathF.Max(0.5f, lineWidth * Scale * 0.5f);
            Shade(cx, cy, MathF.Max(rx, ry) + lineWidth, (px, py, d) =>
            {
                float dx = px + 0.5f - cx * Scale, dy = py + 0.5f - cy * Scale;
                float f = MathF.Sqrt(dx * dx / (ax * ax) + dy * dy / (ay * ay));
                if (f < 1e-5f) return (c, 0f);
                float grad = MathF.Sqrt(dx * dx / (ax * ax * ax * ax) + dy * dy / (ay * ay * ay * ay)) / f;
                float dist = MathF.Abs(f - 1f) / MathF.Max(grad, 1e-5f);
                return (c, Math.Clamp(half + 0.5f - dist, 0f, 1f));
            });
        }

        public void StrokeCircle(float cx, float cy, float r, Rgba c, float lineWidth = 1f) => StrokeEllipse(cx, cy, r, r, c, lineWidth);

        /// <summary>A filled path (<c>moveTo</c> / <c>lineTo</c> / <c>closePath</c> / <c>fill</c>), non-zero winding, 4×4 supersampled.</summary>
        public void FillPolygon(Rgba c, params (float X, float Y)[] points)
        {
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            var p = new (float X, float Y)[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                p[i] = (points[i].X * Scale, points[i].Y * Scale);
                minX = MathF.Min(minX, p[i].X); maxX = MathF.Max(maxX, p[i].X);
                minY = MathF.Min(minY, p[i].Y); maxY = MathF.Max(maxY, p[i].Y);
            }
            for (int py = Math.Max(0, (int)minY); py <= Math.Min(Height - 1, (int)maxY); py++)
            for (int px = Math.Max(0, (int)minX); px <= Math.Min(Width - 1, (int)maxX); px++)
            {
                int inside = 0;
                for (int sy = 0; sy < 4; sy++)
                for (int sx = 0; sx < 4; sx++)
                    if (Winding(p, px + (sx + 0.5f) / 4f, py + (sy + 0.5f) / 4f) != 0) inside++;
                if (inside > 0) Blend(px, py, c, inside / 16f);
            }
        }

        static int Winding((float X, float Y)[] p, float x, float y)
        {
            int w = 0;
            for (int i = 0, j = p.Length - 1; i < p.Length; j = i++)
            {
                var a = p[j]; var b = p[i];
                float side = (b.X - a.X) * (y - a.Y) - (x - a.X) * (b.Y - a.Y);
                if (a.Y <= y) { if (b.Y > y && side > 0) w++; }
                else if (b.Y <= y && side < 0) w--;
            }
            return w;
        }

        /// <summary>Runs <paramref name="shade"/> over every pixel within <paramref name="r"/> (web units) of the centre.</summary>
        void Shade(float cx, float cy, float r, Func<int, int, float, (Rgba, float)> shade)
        {
            float x = cx * Scale, y = cy * Scale, rr = r * Scale + 1f;
            for (int py = Math.Max(0, (int)(y - rr)); py <= Math.Min(Height - 1, (int)(y + rr)); py++)
            for (int px = Math.Max(0, (int)(x - rr)); px <= Math.Min(Width - 1, (int)(x + rr)); px++)
            {
                float dx = px + 0.5f - x, dy = py + 0.5f - y;
                float d = MathF.Sqrt(dx * dx + dy * dy);
                if (d > rr) continue;
                var (c, cover) = shade(px, py, d);
                if (cover > 0f) Blend(px, py, c, cover);
            }
        }

        void Blend(int px, int py, Rgba c, float cover)
        {
            if (px < 0 || py < 0 || px >= Width || py >= Height) return;
            float a = Math.Clamp(c.A * cover, 0f, 1f);
            if (a <= 0f) return;
            int i = (py * Width + px) * 4;
            Pixels[i] = Mix(Pixels[i], c.R, a);
            Pixels[i + 1] = Mix(Pixels[i + 1], c.G, a);
            Pixels[i + 2] = Mix(Pixels[i + 2], c.B, a);
        }

        static byte Mix(byte dst, float src, float a) => (byte)Math.Clamp(MathF.Round(dst + (src * 255f - dst) * a), 0f, 255f);
        static byte B(float v) => (byte)Math.Clamp(MathF.Round(v * 255f), 0f, 255f);
    }
}
