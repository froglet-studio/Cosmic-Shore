using System;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>The painterly look: every dial of the post-process. Floats and colours only.</summary>
    [Serializable]
    public struct PortraitStyle
    {
        [Tooltip("Saturation multiplier after tone mapping.")] public float Saturation;
        [Tooltip("How far shadows shift cool and lights shift warm, 0..1.")] public float WarmCool;
        [Tooltip("Number of value planes the shading is softly quantised into.")] public int ValueSteps;
        [Tooltip("How much of the quantised value is mixed in, 0..1.")] public float Posterise;
        [Tooltip("Brush-stroke texture strength.")] public float Brush;
        [Tooltip("Ink darkening on edges (silhouette and interior).")] public float Ink;
        [Tooltip("Exposure applied before the tone curve.")] public float Exposure;
        [Tooltip("Frame corner cut as a fraction of the side (0 = square, 0.29 = regular octagon).")] public float OctagonCut;
        [Tooltip("Background gradient, top and bottom, before the domain-lit halo.")] public Color BackgroundTop, BackgroundBottom;
        [Tooltip("How much the domain accent lights the background halo behind the head.")] public float HaloAccent;
        public float Vignette;

        public static PortraitStyle Default => new PortraitStyle
        {
            Saturation = 1.40f,
            WarmCool = 0.55f,
            ValueSteps = 9,
            Posterise = 0.22f,
            Brush = 0.12f,
            Ink = 0.42f,
            Exposure = 1.05f,
            OctagonCut = 0.22f,
            BackgroundTop = new Color(0.20f, 0.30f, 0.38f),
            BackgroundBottom = new Color(0.62f, 0.60f, 0.52f),
            HaloAccent = 0.35f,
            Vignette = 0.35f,
        };
    }

    /// <summary>
    /// Turns a lit, smooth-shaded bust render into the painted-illustration read of the shipped
    /// avatar sprites: a soft tone curve, a warm-light / cool-shadow split, softly posterised
    /// value planes, a directional brush texture, ink on the edges, an octagonal frame over a
    /// two-tone background with a domain-lit halo. Pure — a float buffer in, a float buffer out
    /// — so the editor baker and the offline harness run the SAME code. This is the file for
    /// "it doesn't look painted", "too much ink", "the frame is wrong".
    /// </summary>
    public static class PortraitStylizer
    {
        /// <summary>
        /// <paramref name="rgba"/>: width × height × 4 floats, LINEAR premultiplied-free colour
        /// with the bust's coverage in alpha, row 0 at the BOTTOM. Returns the same layout as
        /// sRGB-encoded colour over the framed background, alpha 0 outside the octagon.
        /// </summary>
        public static float[] Apply(float[] rgba, int width, int height, in PortraitStyle style, Color accent, int seed)
        {
            if (rgba == null || rgba.Length != width * height * 4) throw new ArgumentException("PortraitStylizer: buffer size.");
            int n = width * height;
            var lum = new float[n];
            var outp = new float[n * 4];

            // Pass 1: tone, warm/cool, posterise, saturation → working colour; luminance for edges.
            for (int i = 0; i < n; i++)
            {
                float a = rgba[i * 4 + 3];
                float r = rgba[i * 4] * style.Exposure, g = rgba[i * 4 + 1] * style.Exposure, b = rgba[i * 4 + 2] * style.Exposure;
                r = Curve(r); g = Curve(g); b = Curve(b);
                float y = 0.2126f * r + 0.7152f * g + 0.0722f * b;
                // Warm/cool: shadows toward blue-violet, lights toward amber.
                float t = Mathf.Clamp01(y * 1.6f);
                float wc = style.WarmCool;
                r += (t - 0.5f) * 0.10f * wc; g += (t - 0.5f) * 0.03f * wc; b -= (t - 0.5f) * 0.12f * wc;
                // Soft posterise of the value.
                if (style.ValueSteps > 1 && style.Posterise > 0f)
                {
                    float steps = style.ValueSteps;
                    float q = Mathf.Round(y * steps) / steps;
                    float k = Mathf.Lerp(1f, y > 1e-4f ? q / y : 1f, style.Posterise);
                    r *= k; g *= k; b *= k;
                }
                // Saturation.
                float y2 = 0.2126f * r + 0.7152f * g + 0.0722f * b;
                r = y2 + (r - y2) * style.Saturation; g = y2 + (g - y2) * style.Saturation; b = y2 + (b - y2) * style.Saturation;
                outp[i * 4] = Mathf.Max(0f, r); outp[i * 4 + 1] = Mathf.Max(0f, g); outp[i * 4 + 2] = Mathf.Max(0f, b); outp[i * 4 + 3] = a;
                lum[i] = y2 * a;
            }

            // Pass 1b: a soft blur of the working colour — paint has no pores. Weighted so the
            // silhouette (alpha) never bleeds into the background.
            var blurred = new float[n * 4];
            var lumB = new float[n];
            for (int yy = 0; yy < height; yy++)
                for (int xx = 0; xx < width; xx++)
                {
                    float r = 0f, g = 0f, b = 0f, wsum = 0f, l = 0f;
                    for (int dy = -2; dy <= 2; dy++)
                        for (int dx = -2; dx <= 2; dx++)
                        {
                            int sx = Mathf.Clamp(xx + dx, 0, width - 1), sy = Mathf.Clamp(yy + dy, 0, height - 1);
                            int j = (sy * width + sx) * 4;
                            float dl = lum[sy * width + sx] - lum[yy * width + xx];
                            float wgt = (3f - Mathf.Abs(dx) * 0.5f - Mathf.Abs(dy) * 0.5f) * outp[j + 3] * Mathf.Exp(-dl * dl / 0.02f);   // bilateral: edges stay
                            r += outp[j] * wgt; g += outp[j + 1] * wgt; b += outp[j + 2] * wgt; l += lum[sy * width + sx] * wgt; wsum += wgt;
                        }
                    int i = yy * width + xx;
                    if (wsum > 1e-6f) { r /= wsum; g /= wsum; b /= wsum; l /= wsum; }
                    float a = outp[i * 4 + 3];
                    blurred[i * 4] = Mathf.Lerp(outp[i * 4], r, 0.85f);
                    blurred[i * 4 + 1] = Mathf.Lerp(outp[i * 4 + 1], g, 0.85f);
                    blurred[i * 4 + 2] = Mathf.Lerp(outp[i * 4 + 2], b, 0.85f);
                    blurred[i * 4 + 3] = a;
                    lumB[i] = l * a;
                }
            outp = blurred;
            lum = lumB;

            // Pass 2: edges (Sobel on luminance·alpha, plus the alpha edge), brush, frame, background.
            float cut = Mathf.Clamp(style.OctagonCut, 0f, 0.49f) * Mathf.Min(width, height);
            float cx = width * 0.5f, cy = height * 0.5f;
            float haloR = Mathf.Min(width, height) * 0.42f;
            var result = new float[n * 4];
            for (int yy = 0; yy < height; yy++)
                for (int xx = 0; xx < width; xx++)
                {
                    int i = yy * width + xx;
                    // Octagon frame.
                    float ex = Mathf.Abs(xx + 0.5f - cx), ey = Mathf.Abs(yy + 0.5f - cy);
                    bool inside = ex < width * 0.5f && ey < height * 0.5f && (ex + ey) < (width * 0.5f + height * 0.5f - cut);
                    if (!inside) { result[i * 4 + 3] = 0f; continue; }

                    // Edge strength.
                    float gx = S(lum, width, height, xx + 1, yy - 1) + 2f * S(lum, width, height, xx + 1, yy) + S(lum, width, height, xx + 1, yy + 1)
                             - S(lum, width, height, xx - 1, yy - 1) - 2f * S(lum, width, height, xx - 1, yy) - S(lum, width, height, xx - 1, yy + 1);
                    float gy = S(lum, width, height, xx - 1, yy + 1) + 2f * S(lum, width, height, xx, yy + 1) + S(lum, width, height, xx + 1, yy + 1)
                             - S(lum, width, height, xx - 1, yy - 1) - 2f * S(lum, width, height, xx, yy - 1) - S(lum, width, height, xx + 1, yy - 1);
                    float edge = Mathf.Clamp01(Mathf.Sqrt(gx * gx + gy * gy) * 1.6f);
                    edge = GeometryKit.SmoothStep(0.30f, 0.95f, edge);

                    // Brush: streaks along a diagonal, breaking up flat planes.
                    float bx = (xx * 0.55f + yy * 0.83f) / width * 90f, by = (xx * 0.83f - yy * 0.55f) / height * 14f;
                    float brush = (Noise.Fbm(bx, by, 2, 2.2f, 0.5f, seed + 91) - 0.5f) * 2f;
                    float brushK = 1f + style.Brush * brush;

                    float a = outp[i * 4 + 3];
                    float r = outp[i * 4] * brushK * (1f - style.Ink * edge);
                    float g = outp[i * 4 + 1] * brushK * (1f - style.Ink * edge);
                    float b = outp[i * 4 + 2] * brushK * (1f - style.Ink * edge);

                    // Background: gradient + halo behind the head + vignette.
                    float v = yy / (float)(height - 1);
                    Color bg = Color.Lerp(style.BackgroundBottom, style.BackgroundTop, GeometryKit.Smooth(v));
                    float d = Mathf.Sqrt((xx - cx) * (xx - cx) + (yy - cy * 1.15f) * (yy - cy * 1.15f)) / haloR;
                    float halo = GeometryKit.Bell(d / 1.25f);
                    float accentY = 0.2126f * accent.r + 0.7152f * accent.g + 0.0722f * accent.b;
                    Color softAccent = Color.Lerp(new Color(accentY, accentY, accentY), accent, 0.55f);
                    bg = Color.Lerp(bg, softAccent * 0.8f + bg * 0.2f, halo * style.HaloAccent);
                    float bgBrush = 1f + style.Brush * 1.6f * (Noise.Fbm(bx * 0.6f, by * 1.7f, 2, 2f, 0.5f, seed + 92) - 0.5f) * 2f;
                    float edgeDist = Mathf.Min(Mathf.Min(xx, width - 1 - xx), Mathf.Min(yy, height - 1 - yy)) / (float)Mathf.Min(width, height);
                    float vig = 1f - style.Vignette * (1f - GeometryKit.Smooth(edgeDist / 0.22f));
                    bg.r *= bgBrush * vig; bg.g *= bgBrush * vig; bg.b *= bgBrush * vig;

                    // Composite: the bust's silhouette gets a soft ink line from the alpha edge.
                    float aEdge = Mathf.Abs(S(outp, width, height, xx + 1, yy, 3) - S(outp, width, height, xx - 1, yy, 3))
                                + Mathf.Abs(S(outp, width, height, xx, yy + 1, 3) - S(outp, width, height, xx, yy - 1, 3));
                    float ink = Mathf.Clamp01(aEdge) * style.Ink * 0.8f;
                    r = Mathf.Lerp(bg.r, r, a) * (1f - ink);
                    g = Mathf.Lerp(bg.g, g, a) * (1f - ink);
                    b = Mathf.Lerp(bg.b, b, a) * (1f - ink);

                    result[i * 4] = ToSrgb(r); result[i * 4 + 1] = ToSrgb(g); result[i * 4 + 2] = ToSrgb(b); result[i * 4 + 3] = 1f;
                }
            return result;
        }

        /// <summary>A soft filmic shoulder: linear near zero, rolling off toward 1.</summary>
        static float Curve(float x)
        {
            x = Mathf.Max(0f, x);
            float t = x / (1f + x * 0.62f) * 1.28f;          // soft shoulder
            float c = t * t * (3f - 2f * t);                  // and a little S for contrast
            return Mathf.Lerp(t, c, 0.35f);
        }

        static float S(float[] buf, int w, int h, int x, int y, int channel = -1)
        {
            x = Mathf.Clamp(x, 0, w - 1); y = Mathf.Clamp(y, 0, h - 1);
            return channel < 0 ? buf[y * w + x] : buf[(y * w + x) * 4 + channel];
        }

        static float ToSrgb(float v)
        {
            v = Mathf.Clamp01(v);
            return v <= 0.0031308f ? v * 12.92f : 1.055f * Mathf.Pow(v, 1f / 2.4f) - 0.055f;
        }

        public static float FromSrgb(float v)
        {
            v = Mathf.Clamp01(v);
            return v <= 0.04045f ? v / 12.92f : Mathf.Pow((v + 0.055f) / 1.055f, 2.4f);
        }
    }
}
