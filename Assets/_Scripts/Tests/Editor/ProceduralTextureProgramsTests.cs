using System;
using System.IO;
using CosmicShore.Utility;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The genome texture programs against the shipped image files, sampled the same way as
    /// Tools/Build/texture_programs.py "grid64" (64x64 nearest pixel centres); the floors sit just under the
    /// values that script measured (SunsetGradient 55.5 dB, Grad_00 41.1 dB, Circle01_v1 27.9 dB).
    /// The source files are read from disk (PNG through ImageConversion, the TGA by a small reader) because the
    /// imported textures are not CPU-readable.
    /// </summary>
    public class ProceduralTextureProgramsTests
    {
        const string SunsetPath = "Assets/_Graphics/Texture/SunsetGradient.png";
        const string Grad00Path = "Assets/_Graphics/Materials/CrystalMaterials/Textures/Grad_00.tga";
        const string CirclePath = "Assets/Effects Library/Froglet Stuff/Textures/Circle01_v1.png";

        /// <summary>RGBA rows from the TOP, values 0..1.</summary>
        sealed class Image
        {
            public int Width, Height;
            public Color32[] TopDown;
            public Color32 At(int x, int yFromTop) => TopDown[yFromTop * Width + x];
        }

        static string ProjectPath(string assetPath) =>
            Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? "", assetPath);

        static Image LoadPng(string assetPath)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Assert.IsTrue(tex.LoadImage(File.ReadAllBytes(ProjectPath(assetPath))), $"could not decode {assetPath}");
                var bottomUp = tex.GetPixels32();
                int w = tex.width, h = tex.height;
                var top = new Color32[w * h];
                for (int y = 0; y < h; y++) Array.Copy(bottomUp, (h - 1 - y) * w, top, y * w, w);
                return new Image { Width = w, Height = h, TopDown = top };
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tex);
            }
        }

        /// <summary>Uncompressed 24/32-bit truecolour TGA (type 2), either origin.</summary>
        static Image LoadTga(string assetPath)
        {
            var b = File.ReadAllBytes(ProjectPath(assetPath));
            Assert.AreEqual(2, b[2], "only uncompressed truecolour TGA is read here");
            int idLen = b[0], w = b[12] | (b[13] << 8), h = b[14] | (b[15] << 8), bpp = b[16] / 8;
            bool topOrigin = (b[17] & 0x20) != 0;
            int offset = 18 + idLen;
            var top = new Color32[w * h];
            for (int row = 0; row < h; row++)
            {
                int y = topOrigin ? row : h - 1 - row;
                for (int x = 0; x < w; x++)
                {
                    int i = offset + (row * w + x) * bpp;
                    top[y * w + x] = new Color32(b[i + 2], b[i + 1], b[i], bpp == 4 ? b[i + 3] : (byte)255);
                }
            }
            return new Image { Width = w, Height = h, TopDown = top };
        }

        /// <summary>The program's channels from a shipped pixel (texture_programs.py as_channels).</summary>
        static void TargetChannels(Color32 c, ProceduralTexturePrograms.Layout layout, float[] into)
        {
            float r = c.r / 255f, g = c.g / 255f, b = c.b / 255f, a = c.a / 255f;
            switch (layout)
            {
                case ProceduralTexturePrograms.Layout.Rgb: into[0] = r; into[1] = g; into[2] = b; break;
                case ProceduralTexturePrograms.Layout.Grey: into[0] = (r + g + b) / 3f; break;
                default: into[0] = (0.299f * r + 0.587f * g + 0.114f * b) * a; into[1] = a; break;
            }
        }

        static double Grid64Psnr(ProceduralTexturePrograms.Program program, Image img)
        {
            const int n = 64;
            var want = new float[program.Channels];
            var got = new float[program.Channels];
            double sse = 0;
            for (int j = 0; j < n; j++)
            {
                int y = (int)((j + 0.5) / n * img.Height);
                for (int i = 0; i < n; i++)
                {
                    int x = (int)((i + 0.5) / n * img.Width);
                    TargetChannels(img.At(x, y), program.Layout, want);
                    program.EvaluateChannels((x + 0.5) / img.Width, (y + 0.5) / img.Height, got);
                    for (int c = 0; c < program.Channels; c++) sse += (got[c] - want[c]) * (double)(got[c] - want[c]);
                }
            }
            double mse = sse / (n * n * program.Channels);
            return mse < 1e-12 ? 99.0 : 10.0 * Math.Log10(1.0 / mse);
        }

        [Test]
        public void SunsetGradient_MatchesShippedPng() =>
            Assert.GreaterOrEqual(Grid64Psnr(ProceduralTexturePrograms.SunsetGradient, LoadPng(SunsetPath)), 54.0);

        [Test]
        public void Grad00_MatchesShippedTga() =>
            Assert.GreaterOrEqual(Grid64Psnr(ProceduralTexturePrograms.Grad00, LoadTga(Grad00Path)), 40.0);

        [Test]
        public void Circle01V1_MatchesShippedPng() =>
            Assert.GreaterOrEqual(Grid64Psnr(ProceduralTexturePrograms.Circle01V1, LoadPng(CirclePath)), 26.5);

        [Test]
        public void Bake_StoresRowsBottomUp()
        {
            var p = ProceduralTexturePrograms.SunsetGradient;
            var tex = p.Bake(16, 256, mipChain: false);
            try
            {
                Color top = tex.GetPixel(8, 255);
                Color expected = p.Evaluate(8.5 / 16, 0.5 / 256);
                Assert.AreEqual(expected.r, top.r, 1f / 255 + 1e-4f);
                Assert.AreEqual(expected.g, top.g, 1f / 255 + 1e-4f);
                Assert.AreEqual(expected.b, top.b, 1f / 255 + 1e-4f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tex);
            }
        }

        [Test]
        public void GetShared_ReturnsTheSameTexture()
        {
            var a = ProceduralTexturePrograms.GetShared(ProceduralTexturePrograms.Grad00, 256, 4);
            var b = ProceduralTexturePrograms.GetShared(ProceduralTexturePrograms.Grad00, 256, 4);
            Assert.AreSame(a, b);
            Assert.AreEqual(256, a.width);
        }

        [Test]
        public void Program_RejectsUnknownTemplate() =>
            Assert.Throws<ArgumentException>(() => new ProceduralTexturePrograms.Program(
                "bad", new byte[] { 9, 0, 0, 0, 0, 0, 0 }, ProceduralTexturePrograms.Layout.Grey));
    }
}
