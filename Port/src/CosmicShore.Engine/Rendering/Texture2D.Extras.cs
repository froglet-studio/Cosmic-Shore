using System;
using System.IO;
using System.IO.Compression;

namespace CosmicShore.Engine
{
    /// <summary>CPU texture formats (original: UnityEngine.TextureFormat; values match). The port stores RGBA32.</summary>
    public enum TextureFormat
    {
        Alpha8 = 1, ARGB4444 = 2, RGB24 = 3, RGBA32 = 4, ARGB32 = 5, RGB565 = 7, R16 = 9, DXT1 = 10, DXT5 = 12,
        RGBA4444 = 13, BGRA32 = 14, RHalf = 15, RGHalf = 16, RGBAHalf = 17, RFloat = 18, RGFloat = 19, RGBAFloat = 20,
        YUY2 = 21, RGB9e5Float = 22, BC4 = 26, BC5 = 27, BC6H = 24, BC7 = 25, DXT1Crunched = 28, DXT5Crunched = 29,
        ETC_RGB4 = 34, EAC_R = 41, ETC2_RGB = 45, ETC2_RGBA8 = 47, ASTC_4x4 = 48, ASTC_6x6 = 50, RG16 = 62, R8 = 63,
    }

    public enum TextureDimension { Unknown = -1, None = 0, Any = 1, Tex2D = 2, Tex3D = 3, Cube = 4, Tex2DArray = 5, CubeArray = 6 }

    public abstract partial class Texture
    {
        /// <summary>Incremented on every CPU-side content change (original: Texture.updateCount) — the renderer re-uploads on change.</summary>
        public uint updateCount { get; protected set; }
        public void IncrementUpdateCount() => updateCount++;
        public TextureDimension dimension { get; set; } = TextureDimension.Tex2D;
        public bool isReadable { get; set; } = true;
        public float mipMapBias { get; set; }
        public Vector2 texelSize => new(width > 0 ? 1f / width : 0f, height > 0 ? 1f / height : 0f);
        public IntPtr GetNativeTexturePtr() => IntPtr.Zero;
        public static bool allowThreadedTextureCreation { get; set; } = true;
        public static int masterTextureLimit { get; set; }
    }

    public partial class Texture2D
    {
        public TextureFormat format { get; private set; } = TextureFormat.RGBA32;

        public Texture2D(int width, int height, TextureFormat textureFormat, bool mipChain) : this(width, height, textureFormat, mipChain, false) { }

        public Texture2D(int width, int height, TextureFormat textureFormat, bool mipChain, bool linear)
            : this(width, height, new byte[Math.Max(1, width) * Math.Max(1, height) * 4], mipChain)
        {
            format = textureFormat;
            isDataSRGB = !linear;
        }

        public Texture2D(int width, int height, TextureFormat textureFormat, int mipCount, bool linear)
            : this(width, height, textureFormat, mipCount > 1, linear) { }

        byte[] EnsureData()
        {
            var d = GetRawTextureData();
            if (d == null) { d = new byte[width * height * 4]; LoadRawTextureData(d); }
            return d;
        }

        public void SetPixel(int x, int y, Color color)
        {
            if (x < 0 || y < 0 || x >= width || y >= height) return;
            var d = EnsureData();
            Color32 c = color;
            int i = (y * width + x) * 4;
            d[i] = c.r; d[i + 1] = c.g; d[i + 2] = c.b; d[i + 3] = c.a;
        }

        public void SetPixel(int x, int y, Color color, int mipLevel) => SetPixel(x, y, color);

        public Color[] GetPixels() => GetPixels(0, 0, width, height);

        public Color[] GetPixels(int x, int y, int blockWidth, int blockHeight, int miplevel = 0)
        {
            var result = new Color[blockWidth * blockHeight];
            for (int j = 0; j < blockHeight; j++)
                for (int i = 0; i < blockWidth; i++)
                    result[j * blockWidth + i] = GetPixel(x + i, y + j);
            return result;
        }

        public void SetPixels(Color[] colors, int miplevel = 0) => SetPixels(0, 0, width, height, colors, miplevel);

        public void SetPixels(int x, int y, int blockWidth, int blockHeight, Color[] colors, int miplevel = 0)
        {
            for (int j = 0; j < blockHeight; j++)
                for (int i = 0; i < blockWidth; i++)
                    SetPixel(x + i, y + j, colors[j * blockWidth + i]);
        }

        public Color GetPixelBilinear(float u, float v)
        {
            float fx = u * width - 0.5f, fy = v * height - 0.5f;
            int x0 = (int)MathF.Floor(fx), y0 = (int)MathF.Floor(fy);
            float tx = fx - x0, ty = fy - y0;
            Color a = GetPixel(x0, y0), b = GetPixel(x0 + 1, y0), c = GetPixel(x0, y0 + 1), d = GetPixel(x0 + 1, y0 + 1);
            return Color.Lerp(Color.Lerp(a, b, tx), Color.Lerp(c, d, tx), ty);
        }

        public void Apply() => Apply(true, false);

        /// <summary>Resize (original: Reinitialize) — contents become undefined (cleared).</summary>
        public bool Reinitialize(int width, int height) => Reinitialize(width, height, format, false);

        public bool Reinitialize(int width, int height, TextureFormat format, bool hasMipMap)
        {
            this.width = width; this.height = height; this.format = format;
            LoadRawTextureData(new byte[width * height * 4]);
            IncrementUpdateCount();
            return true;
        }

        public bool Resize(int width, int height) => Reinitialize(width, height);

        /// <summary>
        /// Reads a rectangle of the ACTIVE render target (RenderTexture.active, or the back buffer) into
        /// this texture at (destX, destY). The renderer installs <see cref="ReadPixelsHook"/>; without it
        /// the region reads black.
        /// </summary>
        public void ReadPixels(Rect source, int destX, int destY, bool recalculateMipMaps = true)
        {
            int w = (int)source.width, h = (int)source.height;
            var rgba = ReadPixelsHook?.Invoke(RenderTexture.active, (int)source.x, (int)source.y, w, h);
            var d = EnsureData();
            for (int j = 0; j < h; j++)
            {
                int ty = destY + j;
                if (ty < 0 || ty >= height) continue;
                for (int i = 0; i < w; i++)
                {
                    int tx = destX + i;
                    if (tx < 0 || tx >= width) continue;
                    int dst = (ty * width + tx) * 4;
                    if (rgba == null) { d[dst] = d[dst + 1] = d[dst + 2] = 0; d[dst + 3] = 255; continue; }
                    int src = (j * w + i) * 4;
                    Buffer.BlockCopy(rgba, src, d, dst, 4);
                }
            }
        }

        /// <summary>Renderer hook: (target or null = back buffer, x, y, w, h) → RGBA32 bottom-up.</summary>
        public static Func<RenderTexture, int, int, int, int, byte[]> ReadPixelsHook;

        public static Texture2D blackTexture { get; } = Solid(0, 0, 0, 255, "UnityBlack");
        public static Texture2D grayTexture { get; } = Solid(128, 128, 128, 255, "UnityGrey");
        public static Texture2D normalTexture { get; } = Solid(128, 128, 255, 255, "UnityNormalMap");
        public static Texture2D redTexture { get; } = Solid(255, 0, 0, 255, "UnityRed");

        static Texture2D Solid(byte r, byte g, byte b, byte a, string n)
        {
            var data = new byte[4 * 4 * 4];
            for (int i = 0; i < data.Length; i += 4) { data[i] = r; data[i + 1] = g; data[i + 2] = b; data[i + 3] = a; }
            return new Texture2D(4, 4, data) { name = n, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        }
    }

    /// <summary>Image encode/decode (original: UnityEngine.ImageConversion extension methods).</summary>
    public static class ImageConversion
    {
        /// <summary>Decoder installed by the content layer (PNG/JPG/TGA/PSD → RGBA32 bottom-up).</summary>
        public static Func<byte[], (int width, int height, byte[] rgba32BottomUp)?> Decoder;

        public static bool LoadImage(this Texture2D tex, byte[] data, bool markNonReadable = false)
        {
            var decoded = data == null ? null : Decoder?.Invoke(data);
            if (decoded == null) return false;
            var (w, h, px) = decoded.Value;
            tex.width = w; tex.height = h;
            tex.LoadRawTextureData(px);
            tex.IncrementUpdateCount();
            return true;
        }

        /// <summary>Encodes RGBA32 PNG (8-bit, non-interlaced, zlib/deflate).</summary>
        public static byte[] EncodeToPNG(this Texture2D tex)
        {
            int w = tex.width, h = tex.height;
            var px = tex.GetRawTextureData() ?? new byte[w * h * 4];
            var raw = new byte[(w * 4 + 1) * h];
            for (int y = 0; y < h; y++)
            {
                int dst = y * (w * 4 + 1);
                raw[dst] = 0; // filter: none
                Buffer.BlockCopy(px, (h - 1 - y) * w * 4, raw, dst + 1, w * 4); // PNG rows are top-down
            }

            using var ms = new MemoryStream();
            ms.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
            var ihdr = new byte[13];
            WriteBE(ihdr, 0, (uint)w); WriteBE(ihdr, 4, (uint)h);
            ihdr[8] = 8; ihdr[9] = 6; ihdr[10] = 0; ihdr[11] = 0; ihdr[12] = 0;
            Chunk(ms, "IHDR", ihdr);
            using (var z = new MemoryStream())
            {
                using (var zs = new ZLibStream(z, CompressionLevel.Fastest, leaveOpen: true)) zs.Write(raw, 0, raw.Length);
                Chunk(ms, "IDAT", z.ToArray());
            }
            Chunk(ms, "IEND", Array.Empty<byte>());
            return ms.ToArray();
        }

        public static byte[] EncodeToJPG(this Texture2D tex, int quality = 75) => EncodeToPNG(tex);
        public static byte[] EncodeToTGA(this Texture2D tex) => EncodeToPNG(tex);

        static void Chunk(Stream s, string type, byte[] data)
        {
            var len = new byte[4]; WriteBE(len, 0, (uint)data.Length);
            s.Write(len);
            var t = System.Text.Encoding.ASCII.GetBytes(type);
            s.Write(t);
            s.Write(data);
            uint crc = Crc32(t, 0xFFFFFFFFu);
            crc = Crc32(data, crc) ^ 0xFFFFFFFFu;
            var c = new byte[4]; WriteBE(c, 0, crc);
            s.Write(c);
        }

        static void WriteBE(byte[] b, int o, uint v) { b[o] = (byte)(v >> 24); b[o + 1] = (byte)(v >> 16); b[o + 2] = (byte)(v >> 8); b[o + 3] = (byte)v; }

        static uint[] s_Table;
        static uint Crc32(byte[] data, uint crc)
        {
            if (s_Table == null)
            {
                s_Table = new uint[256];
                for (uint n = 0; n < 256; n++)
                {
                    uint c = n;
                    for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                    s_Table[n] = c;
                }
            }
            foreach (var b in data) crc = s_Table[(crc ^ b) & 0xFF] ^ (crc >> 8);
            return crc;
        }
    }

    /// <summary>Screenshots (original: UnityEngine.ScreenCapture) through the renderer's read-back hook.</summary>
    public static class ScreenCapture
    {
        public enum StereoScreenCaptureMode { LeftEye = 1, RightEye = 2, BothEyes = 3 }

        public static Texture2D CaptureScreenshotAsTexture(int superSize = 1)
        {
            var tex = new Texture2D(Screen.width, Screen.height, TextureFormat.RGBA32, false);
            var prev = RenderTexture.active;
            RenderTexture.active = null;
            tex.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
            RenderTexture.active = prev;
            tex.Apply();
            return tex;
        }

        public static void CaptureScreenshot(string filename, int superSize = 1)
            => File.WriteAllBytes(Path.IsPathRooted(filename) ? filename : Path.Combine(Application.persistentDataPath, filename),
                                  CaptureScreenshotAsTexture(superSize).EncodeToPNG());

        public static void CaptureScreenshotIntoRenderTexture(RenderTexture renderTexture) { }
    }
}
