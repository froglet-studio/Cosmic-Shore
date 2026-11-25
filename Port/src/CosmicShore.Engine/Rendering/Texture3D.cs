using System;
using CosmicShore.Engine.Experimental.Rendering;

namespace CosmicShore.Engine.Experimental.Rendering
{
    /// <summary>Flags for creating a texture from a <see cref="GraphicsFormat"/> (original: UnityEngine.Experimental.Rendering.TextureCreationFlags; values match).</summary>
    [Flags]
    public enum TextureCreationFlags
    {
        None = 0,
        MipChain = 1 << 0,
        DontInitializePixels = 1 << 2,
        Crunch = 1 << 6,
        DontUploadUponCreate = 1 << 10,
        IgnoreMipmapLimit = 1 << 11,
    }
}

namespace CosmicShore.Engine
{
    /// <summary>
    /// A volume texture (original: UnityEngine.Texture3D). The port keeps its pixels on the CPU in the same layout as
    /// <see cref="Texture2D"/> - tightly packed RGBA32, x fastest, then y (bottom-up), then z - so it can be read back
    /// and tested. Formats other than 8-bit RGBA are stored as RGBA32 too.
    ///
    /// PLACEHOLDER: the renderer does not upload volume textures yet, so a shader that samples one (the colour grade's
    /// <c>ColorGrade.cginc</c>) sees nothing bound. The game only switches that path on when a non-Photic colour set is
    /// worn, so Photic, the default, draws exactly as before.
    /// </summary>
    public class Texture3D : Texture
    {
        byte[] m_Data;

        /// <summary>Depth in voxels (original contract).</summary>
        public int depth { get; private set; }

        /// <summary>The format the texture was created with (original contract: Texture.graphicsFormat).</summary>
        public GraphicsFormat graphicsFormat { get; }

        public Texture3D(int width, int height, int depth, GraphicsFormat format, TextureCreationFlags flags)
            : this(width, height, depth, format, flags, (flags & TextureCreationFlags.MipChain) != 0 ? -1 : 1) { }

        public Texture3D(int width, int height, int depth, GraphicsFormat format, TextureCreationFlags flags, int mipCount)
        {
            Init(width, height, depth);
            graphicsFormat = format;
            isDataSRGB = format == GraphicsFormat.R8G8B8A8_SRGB || format == GraphicsFormat.R8_SRGB;
            if (mipCount != 1) mipmapCount = 1 + (int)Math.Floor(Math.Log2(Math.Max(width, Math.Max(height, depth))));
        }

        public Texture3D(int width, int height, int depth, TextureFormat textureFormat, bool mipChain)
        {
            Init(width, height, depth);
            graphicsFormat = GraphicsFormat.R8G8B8A8_UNorm;
            if (mipChain) mipmapCount = 1 + (int)Math.Floor(Math.Log2(Math.Max(width, Math.Max(height, depth))));
        }

        void Init(int w, int h, int d)
        {
            if (w <= 0 || h <= 0 || d <= 0) throw new ArgumentOutOfRangeException(nameof(width), "Texture3D dimensions must be positive");
            width = w; height = h; depth = d;
            dimension = TextureDimension.Tex3D;
            m_Data = new byte[w * h * d * 4];
        }

        /// <summary>The backing RGBA32 buffer (width*height*depth*4 bytes), by reference.</summary>
        public byte[] GetRawTextureData() => m_Data;

        /// <summary>
        /// Original contract: copies raw pixel data into mip <paramref name="mipLevel"/>. Only mip 0 is stored; the data
        /// is taken as RGBA32 bytes (the layout the game writes for R8G8B8A8 formats).
        /// </summary>
        public void SetPixelData<T>(T[] data, int mipLevel, int sourceDataStartIndex = 0) where T : struct
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (mipLevel != 0) return;
            var bytes = System.Runtime.InteropServices.MemoryMarshal.AsBytes(data.AsSpan(sourceDataStartIndex));
            if (bytes.Length < m_Data.Length)
                throw new ArgumentException($"Texture3D.SetPixelData: {bytes.Length} bytes for a {width}x{height}x{depth} RGBA32 texture ({m_Data.Length} needed)");
            bytes.Slice(0, m_Data.Length).CopyTo(m_Data);
        }

        /// <summary>Original contract: every voxel, x fastest, then y, then z.</summary>
        public Color32[] GetPixels32(int miplevel = 0)
        {
            var result = new Color32[width * height * depth];
            for (int i = 0; i < result.Length; i++)
                result[i] = new Color32(m_Data[i * 4], m_Data[i * 4 + 1], m_Data[i * 4 + 2], m_Data[i * 4 + 3]);
            return result;
        }

        public void SetPixels32(Color32[] colors, int miplevel = 0)
        {
            if (colors == null || colors.Length != width * height * depth) throw new ArgumentException($"expected {width * height * depth} colors");
            if (miplevel != 0) return;
            for (int i = 0; i < colors.Length; i++)
            {
                m_Data[i * 4] = colors[i].r; m_Data[i * 4 + 1] = colors[i].g; m_Data[i * 4 + 2] = colors[i].b; m_Data[i * 4 + 3] = colors[i].a;
            }
        }

        /// <summary>The voxel at (x, y, z), clamped to the texture.</summary>
        public Color GetPixel(int x, int y, int z)
        {
            x = Math.Clamp(x, 0, width - 1); y = Math.Clamp(y, 0, height - 1); z = Math.Clamp(z, 0, depth - 1);
            int i = ((z * height + y) * width + x) * 4;
            return new Color32(m_Data[i], m_Data[i + 1], m_Data[i + 2], m_Data[i + 3]);
        }

        public void SetPixel(int x, int y, int z, Color color)
        {
            if (x < 0 || y < 0 || z < 0 || x >= width || y >= height || z >= depth) return;
            Color32 c = color;
            int i = ((z * height + y) * width + x) * 4;
            m_Data[i] = c.r; m_Data[i + 1] = c.g; m_Data[i + 2] = c.b; m_Data[i + 3] = c.a;
        }

        /// <summary>Original contract: uploads pending CPU edits. The port has no volume upload yet (see the class note).</summary>
        public void Apply(bool updateMipmaps = true, bool makeNoLongerReadable = false)
        {
            IncrementUpdateCount();
            if (makeNoLongerReadable) isReadable = false;
        }
    }
}
