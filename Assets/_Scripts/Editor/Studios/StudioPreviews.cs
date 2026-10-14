using System;
using Rgba = CosmicShore.Editor.Studios.StudioPreviewCanvas.Rgba;

namespace CosmicShore.Editor.Studios
{
    /// <summary>
    /// Each studio's looping card preview, line for line the web hub's (<c>Docs/Studios/VesselStudio/index.html</c>,
    /// <c>preview('pvSquirrel' | 'pvStoat', …)</c>), so the Vessel Studio home in Unity shows the same little flights as
    /// the artifact. A studio without its own preview gets <see cref="Generic"/> in its accent. When a studio's web
    /// preview changes, change it here too.
    /// </summary>
    public static class StudioPreviews
    {
        static readonly Rgba Night = Rgba.Hex("#05060c");

        /// <summary>Whether Unity has a line-for-line port of this studio's hub preview (else the home shows its baked thumbnail).</summary>
        public static bool Has(string id) => id is "squirrel" or "stoat";

        /// <summary>Draws studio <paramref name="id"/> at <paramref name="t"/> seconds. <paramref name="domain"/> is the game's domain colours (studio-domains.js).</summary>
        public static void Draw(string id, StudioPreviewCanvas g, float t, Func<string, Rgba> domain, Rgba accent)
        {
            switch (id)
            {
                case "squirrel": Squirrel(g, t, domain); break;
                case "stoat": Stoat(g, t); break;
                default: Generic(g, t, accent); break;
            }
        }

        static void Squirrel(StudioPreviewCanvas g, float t, Func<string, Rgba> domain)
        {
            float w = g.W, h = g.H;
            g.Clear(Night);
            for (int lane = 0; lane < 2; lane++)   // rival trails
            {
                var c = (lane == 1 ? domain("gold") : domain("ruby")).WithAlpha(0.8f);
                for (int i = 0; i < 40; i++)
                {
                    float x = ((i * 34 - t * 140) % (w + 60) + w + 60) % (w + 60) - 30;
                    float y = h * (0.38f + lane * 0.26f) + MathF.Sin(i * 0.7f + lane) * 8;
                    g.FillRect(x, y, 16, 4, c);
                }
            }
            var jade = domain("jade");
            float sx = w * 0.42f, sy = h * 0.5f + MathF.Sin(t * 1.3f) * h * 0.18f;
            g.StrokeCircle(sx, sy, 34, jade.WithAlpha(0x55 / 255f));   // skimmer
            for (int i = 1; i < 14; i++)
                g.FillRect(sx - i * 18, h * 0.5f + MathF.Sin((t - i * 0.12f) * 1.3f) * h * 0.18f - 2, 10, 4, jade.WithAlpha(0.5f - i * 0.03f));
            g.FillPolygon(jade, (sx + 18, sy), (sx - 10, sy - 9), (sx - 4, sy), (sx - 10, sy + 9));
            float rx = w * 0.82f, ry = h * 0.5f;   // a boost ring ahead
            var ring = Rgba.Hex("#ff6a4d");
            for (int i = 0; i < 8; i++)
            {
                float a = i / 8f * MathF.PI * 2 + t * 0.5f;
                g.FillRect(rx + MathF.Cos(a) * 9 - 3, ry + MathF.Sin(a) * 28 - 3, 6, 6, ring);
            }
        }

        static readonly (float, Rgba)[] BlackHole =
        {
            (0f, Rgba.Hex("#000")), (0.25f, Rgba.Hex("#000")), (0.32f, Rgba.Hex("#ffcf8a")),
            (0.5f, Rgba.Hex("#c48bff33")), (1f, Rgba.Hex("#05060c00")),
        };
        static readonly (float, Rgba)[] WhiteHole = { (0f, Rgba.Hex("#fff")), (1f, Rgba.Hex("#ffffff00")) };

        static void Stoat(StudioPreviewCanvas g, float t)
        {
            float w = g.W, h = g.H;
            g.Clear(Night);
            float bx = w * 0.55f, by = h * 0.42f, wx = w * 0.55f, wy = h * 0.78f;
            g.FillRadial(bx, by, 60, 4, 60, BlackHole);
            g.FillRadial(wx, wy, 26, 1, 26, WhiteHole);
            float a = t * 1.6f, r = 46;   // the orbit
            float px = bx + MathF.Cos(a) * r * 1.6f, py = by + MathF.Sin(a) * r * 0.6f;
            g.StrokeEllipse(bx, by, r * 1.6f, r * 0.6f, Rgba.Hex("#c48bff66"));
            g.FillCircle(px, py, 6, Rgba.Hex("#c48bff"));
        }

        /// <summary>A hull weaving through the night with its wake, for a studio that has no preview of its own yet.</summary>
        static void Generic(StudioPreviewCanvas g, float t, Rgba accent)
        {
            float w = g.W, h = g.H;
            g.Clear(Night);
            float sx = w * 0.45f, sy = h * 0.5f + MathF.Sin(t * 1.3f) * h * 0.2f;
            for (int i = 1; i < 14; i++)
                g.FillRect(sx - i * 18, h * 0.5f + MathF.Sin((t - i * 0.12f) * 1.3f) * h * 0.2f - 2, 10, 4, accent.WithAlpha(0.5f - i * 0.03f));
            g.FillPolygon(accent, (sx + 18, sy), (sx - 10, sy - 9), (sx - 4, sy), (sx - 10, sy + 9));
        }
    }
}
