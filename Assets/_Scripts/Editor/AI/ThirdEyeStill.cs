using UnityEngine;

namespace CosmicShore.Editor.AI
{
    /// <summary>
    /// Draws the Third Eye's overlay (trails, aim lines, target rings) into a still's pixels, pure: the window's own
    /// overlay is drawn with Handles in GUI space, so a capture of its texture alone would leave out what the AI is
    /// thinking. Pixel coordinates have their origin at the bottom left, as a <see cref="Texture2D"/>'s and a
    /// viewport's do. <c>ThirdEyeStillTests</c> pins it.
    /// </summary>
    public static class ThirdEyeStill
    {
        /// <summary>A line from <paramref name="a"/> to <paramref name="b"/>, <paramref name="width"/> pixels wide.</summary>
        public static void Line(Color32[] px, int w, int h, Vector2 a, Vector2 b, Color color, float width)
        {
            if (px == null || px.Length < w * h || w <= 0 || h <= 0) return;
            int r = Mathf.Max(0, Mathf.RoundToInt(width * 0.5f - 0.5f));
            // Cut to the image first: a line whose far end projects thousands of pixels away (a near-plane clip) would
            // otherwise spend its steps off the image and come out dotted.
            if (!Clip(ref a, ref b, -r - 1f, -r - 1f, w + r, h + r)) return;
            int steps = Mathf.Clamp(Mathf.CeilToInt(Vector2.Distance(a, b)), 1, 8192);
            for (int i = 0; i <= steps; i++) Dot(px, w, h, Vector2.Lerp(a, b, i / (float)steps), r, color);
        }

        /// <summary>Liang-Barsky: the part of a..b inside the box, false when none of it is.</summary>
        public static bool Clip(ref Vector2 a, ref Vector2 b, float xMin, float yMin, float xMax, float yMax)
        {
            float t0 = 0f, t1 = 1f, dx = b.x - a.x, dy = b.y - a.y;
            float[] p = { -dx, dx, -dy, dy };
            float[] q = { a.x - xMin, xMax - a.x, a.y - yMin, yMax - a.y };
            for (int i = 0; i < 4; i++)
            {
                if (Mathf.Approximately(p[i], 0f))
                {
                    if (q[i] < 0f) return false;
                    continue;
                }
                float t = q[i] / p[i];
                if (p[i] < 0f) { if (t > t1) return false; if (t > t0) t0 = t; }
                else { if (t < t0) return false; if (t < t1) t1 = t; }
            }
            Vector2 from = a + (b - a) * t0, to = a + (b - a) * t1;
            a = from;
            b = to;
            return true;
        }

        /// <summary>A circle of <paramref name="radius"/> pixels round <paramref name="center"/>.</summary>
        public static void Ring(Color32[] px, int w, int h, Vector2 center, float radius, Color color, float width)
        {
            if (px == null || px.Length < w * h || w <= 0 || h <= 0 || radius <= 0f) return;
            int r = Mathf.Max(0, Mathf.RoundToInt(width * 0.5f - 0.5f));
            int steps = Mathf.Clamp(Mathf.CeilToInt(2f * Mathf.PI * radius), 8, 4096);
            for (int i = 0; i < steps; i++)
            {
                float a = i * 2f * Mathf.PI / steps;
                Dot(px, w, h, center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius, r, color);
            }
        }

        /// <summary>A filled disc (the end of an aim line).</summary>
        public static void Disc(Color32[] px, int w, int h, Vector2 center, float radius, Color color)
        {
            if (px == null || px.Length < w * h || w <= 0 || h <= 0) return;
            Dot(px, w, h, center, Mathf.Max(0, Mathf.RoundToInt(radius)), color);
        }

        /// <summary>Viewport (0..1, y up) to pixel coordinates.</summary>
        public static Vector2 ToPixel(Vector3 viewport, int w, int h) => new(viewport.x * w, viewport.y * h);

        static void Dot(Color32[] px, int w, int h, Vector2 p, int r, Color color)
        {
            int cx = Mathf.RoundToInt(p.x), cy = Mathf.RoundToInt(p.y);
            for (int dy = -r; dy <= r; dy++)
            for (int dx = -r; dx <= r; dx++)
            {
                if (r > 0 && dx * dx + dy * dy > r * r) continue;
                int x = cx + dx, y = cy + dy;
                if (x < 0 || y < 0 || x >= w || y >= h) continue;
                int i = y * w + x;
                px[i] = Blend(px[i], color);
            }
        }

        /// <summary><paramref name="over"/> laid on <paramref name="under"/> by its alpha; the result is opaque.</summary>
        public static Color32 Blend(Color32 under, Color over)
        {
            float a = Mathf.Clamp01(over.a);
            return new Color32(
                (byte)Mathf.RoundToInt(under.r * (1f - a) + Mathf.Clamp01(over.r) * 255f * a),
                (byte)Mathf.RoundToInt(under.g * (1f - a) + Mathf.Clamp01(over.g) * 255f * a),
                (byte)Mathf.RoundToInt(under.b * (1f - a) + Mathf.Clamp01(over.b) * 255f * a),
                255);
        }
    }
}
