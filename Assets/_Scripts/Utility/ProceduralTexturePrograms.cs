using System;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// Genome texture programs: a few dozen bytes that rebuild a shipped texture exactly (a field op plus a
    /// least-squares LUT), baked at load into a small cached texture instead of shipping a megabyte image.
    /// The byte format and the ops are a line-for-line port of the compression study's pops.py / fit.py and of
    /// <c>Tools/Build/texture_programs.py</c>, which measures each program against the shipped file.
    /// Doc: <c>Docs/GENOME_TEXTURE_PROGRAMS.md</c>.
    ///
    /// Program bytes: [template id][p0][p1][p2][LUT: knots x channels bytes][seed].
    /// Template 6 = tex.gradient (linear), 7 = tex.gradient.radial. The LUT is K uniform knots over [0,1] read
    /// with linear interpolation (tex.curve). v is measured from the TOP row of the source image.
    /// </summary>
    public static class ProceduralTexturePrograms
    {
        public enum Layout
        {
            /// <summary>Three colour channels, opaque.</summary>
            Rgb = 0,
            /// <summary>One grey channel written to r, g and b, opaque.</summary>
            Grey = 1,
            /// <summary>Premultiplied grey plus alpha (a sprite); baked as straight colour plus alpha.</summary>
            PremultipliedGreyAlpha = 2,
        }

        public const byte LinearGradient = 6;
        public const byte RadialGradient = 7;

        /// <summary>One decoded program.</summary>
        public sealed class Program
        {
            public string Name { get; }
            public Layout Layout { get; }
            public byte Template { get; }
            public int Channels { get; }
            public int Knots { get; }

            readonly float[] lut;
            readonly double p0, p1, p2;

            // Linear gradient parameters.
            readonly double cosA, sinA, length, pos;
            // Radial gradient parameters.
            readonly double cx, cy;

            public Program(string name, byte[] bytes, Layout layout)
            {
                if (bytes == null || bytes.Length < 6)
                    throw new ArgumentException($"Texture program '{name}' is too short.", nameof(bytes));
                Name = name;
                Layout = layout;
                Template = bytes[0];
                Channels = layout switch { Layout.Rgb => 3, Layout.Grey => 1, _ => 2 };
                int lutBytes = bytes.Length - 5;
                if (lutBytes % Channels != 0)
                    throw new ArgumentException($"Texture program '{name}': LUT of {lutBytes} bytes is not a multiple of {Channels} channels.");
                Knots = lutBytes / Channels;
                if (Knots < 2)
                    throw new ArgumentException($"Texture program '{name}' needs at least two LUT knots.");

                lut = new float[lutBytes];
                for (int i = 0; i < lutBytes; i++) lut[i] = bytes[4 + i] / 255f;

                p0 = bytes[1] / 255.0;
                p1 = bytes[2] / 255.0;
                p2 = bytes[3] / 255.0;
                switch (Template)
                {
                    case LinearGradient:
                        double angle = 2.0 * Math.PI * p0;   // P("angle", lin, 0, 2pi)
                        cosA = Math.Cos(angle);
                        sinA = Math.Sin(angle);
                        pos = -1.0 + 2.0 * p1;               // P("pos", lin, -1, 1)
                        length = 0.25 * Math.Pow(16.0, p2);  // P("length", exp, 0.25, 4)
                        break;
                    case RadialGradient:
                        cx = 0.4 + 0.2 * p0;                 // P("cx", lin, 0.4, 0.6)
                        cy = 0.4 + 0.2 * p1;                 // P("cy", lin, 0.4, 0.6)
                        length = 0.5 * Math.Pow(4.0, p2);    // P("length", exp, 0.5, 2)
                        break;
                    default:
                        throw new ArgumentException($"Texture program '{name}': unknown template {Template}.");
                }
            }

            /// <summary>The field op at (u, vFromTop), clamped to [0,1].</summary>
            public double Field(double u, double vFromTop)
            {
                double g = Template == LinearGradient
                    ? ((u - 0.5) * cosA + (vFromTop - 0.5) * sinA) / length + 0.5 + pos * 0.5
                    : Math.Sqrt((u - cx) * (u - cx) + (vFromTop - cy) * (vFromTop - cy)) * 2.0 / length;
                return g < 0 ? 0 : g > 1 ? 1 : g;
            }

            /// <summary>The program's raw channel values (its layout) at (u, vFromTop), written into <paramref name="into"/>.</summary>
            public void EvaluateChannels(double u, double vFromTop, float[] into)
            {
                double t = Field(u, vFromTop) * (Knots - 1);
                int i = Math.Min((int)Math.Floor(t), Knots - 2);
                float f = (float)(t - i);
                for (int c = 0; c < Channels; c++)
                {
                    float a = lut[i * Channels + c];
                    float b = lut[(i + 1) * Channels + c];
                    into[c] = a + (b - a) * f;
                }
            }

            /// <summary>The colour the shipped texture holds at (u, vFromTop).</summary>
            public Color Evaluate(double u, double vFromTop)
            {
                var ch = new float[Channels];
                EvaluateChannels(u, vFromTop, ch);
                return ToColor(ch);
            }

            Color ToColor(float[] ch)
            {
                switch (Layout)
                {
                    case Layout.Rgb: return new Color(ch[0], ch[1], ch[2], 1f);
                    case Layout.Grey: return new Color(ch[0], ch[0], ch[0], 1f);
                    default:
                        float alpha = ch[1];
                        float grey = alpha > 1e-6f ? Mathf.Clamp01(ch[0] / alpha) : 0f;
                        return new Color(grey, grey, grey, alpha);
                }
            }

            /// <summary>Bake into a new texture (rows bottom-up, as Unity stores them). The caller owns it.</summary>
            public Texture2D Bake(int width, int height, bool mipChain = true)
            {
                var tex = new Texture2D(width, height, TextureFormat.RGBA32, mipChain, linear: false)
                {
                    name = $"{Name} (program {width}x{height})",
                    wrapMode = TextureWrapMode.Repeat, // matches the shipped imports (wrapU 0 = Repeat)
                    filterMode = FilterMode.Bilinear,
                };
                var pixels = new Color32[width * height];
                var ch = new float[Channels];
                for (int y = 0; y < height; y++)
                {
                    double vFromTop = 1.0 - (y + 0.5) / height;
                    for (int x = 0; x < width; x++)
                    {
                        EvaluateChannels((x + 0.5) / width, vFromTop, ch);
                        pixels[y * width + x] = ToColor(ch);
                    }
                }
                tex.SetPixels32(pixels);
                tex.Apply(mipChain, makeNoLongerReadable: false);
                return tex;
            }
        }

        // The fitted programs (Docs/GENOME_TEXTURE_PROGRAMS.md lists where each came from and how well it matches).
        public const string SunsetGradientHex =
            "063f9f9d000000000000000000ee3095ee3594ee3e91ef498bef5485ef5f7df06a74f0736af07c5ff08453f08b47f0903af0942f00";
        public const string Grad00Hex = "06ff80650002132e537c9eb2b7b09a774e2a100000";
        public const string Circle01V1Hex =
            "078080aa404040403f3f3e3e3d3d3c3c3b3b393937373535333331312f2f2c2c29292727242422221f1f1d1d1b1b181816161414121210100f0f0d0d0c0c0a0a09090808070706060505050509090f0f16161f1f26263d3d5353e4e40000040400000101000000000000000000000000000000000000000000000000000000000000000000";

        public static readonly Program SunsetGradient = new("SunsetGradient", FromHex(SunsetGradientHex), Layout.Rgb);
        public static readonly Program Grad00 = new("Grad_00", FromHex(Grad00Hex), Layout.Grey);
        public static readonly Program Circle01V1 = new("Circle01_v1", FromHex(Circle01V1Hex), Layout.PremultipliedGreyAlpha);

        /// <summary>The proposed baked sizes (measured by texture_programs.py "baked").</summary>
        public static Texture2D SunsetGradientTexture => GetShared(SunsetGradient, 16, 256);
        public static Texture2D Grad00Texture => GetShared(Grad00, 256, 4);
        public static Texture2D Circle01V1Texture => GetShared(Circle01V1, 256, 256);

        static readonly Dictionary<(Program, int, int), Texture2D> Cache = new();

        /// <summary>A baked texture shared by every caller asking for the same program and size.</summary>
        public static Texture2D GetShared(Program program, int width, int height)
        {
            var key = (program, width, height);
            if (Cache.TryGetValue(key, out var tex) && tex) return tex;
            tex = program.Bake(width, height);
            tex.hideFlags = HideFlags.DontSave;
            Cache[key] = tex;
            return tex;
        }

        public static byte[] FromHex(string hex)
        {
            if (hex == null || hex.Length % 2 != 0)
                throw new ArgumentException("Hex program must have an even number of digits.", nameof(hex));
            var bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            return bytes;
        }
    }
}
