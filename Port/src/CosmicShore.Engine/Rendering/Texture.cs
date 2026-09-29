using System;

namespace CosmicShore.Engine
{
    /// <summary>Original contract: texture sampling filter. Numeric values are the serialized ones.</summary>
    public enum FilterMode
    {
        Point = 0,
        Bilinear = 1,
        Trilinear = 2,
    }

    /// <summary>Original contract: texture coordinate wrapping. Numeric values are the serialized ones.</summary>
    public enum TextureWrapMode
    {
        Repeat = 0,
        Clamp = 1,
        Mirror = 2,
        MirrorOnce = 3,
    }

    /// <summary>
    /// Base texture asset (UI arc B2). Dimensions are the data the headless engine consumes
    /// (RawImage.SetNativeSize, Sprite geometry); Arc E adds the sampler state an importer
    /// or a GPU upload needs (<see cref="filterMode"/>, <see cref="wrapModeU"/>/<see cref="wrapModeV"/>).
    /// </summary>
    public abstract partial class Texture : Object
    {
        public virtual int width { get; set; }
        public virtual int height { get; set; }

        /// <summary>Sampling filter (original default: Bilinear).</summary>
        public FilterMode filterMode { get; set; } = FilterMode.Bilinear;

        /// <summary>Horizontal (U) wrap (original default: Repeat).</summary>
        public TextureWrapMode wrapModeU { get; set; } = TextureWrapMode.Repeat;

        /// <summary>Vertical (V) wrap (original default: Repeat).</summary>
        public TextureWrapMode wrapModeV { get; set; } = TextureWrapMode.Repeat;

        /// <summary>Original contract: reads the U wrap mode; writing sets both U and V.</summary>
        public TextureWrapMode wrapMode
        {
            get => wrapModeU;
            set { wrapModeU = value; wrapModeV = value; }
        }

        /// <summary>Anisotropic filtering level (original default: 1).</summary>
        public int anisoLevel { get; set; } = 1;

        /// <summary>Mip-level count (1 = no mip chain). A GPU upload generates the chain when &gt; 1.</summary>
        public virtual int mipmapCount { get; protected set; } = 1;

        /// <summary>
        /// True when the stored colour bytes are sRGB-encoded and a GPU upload should use an
        /// sRGB format (the importer's <c>sRGBTexture</c> flag). Alpha is always linear.
        /// </summary>
        public bool isDataSRGB { get; set; } = true;
    }

    /// <summary>
    /// 2D texture asset. Arc B2 made it a named (width, height) reference; Arc E gives it
    /// optional RGBA32 pixel storage so real Unity textures can be decoded, sampled
    /// (debug rasterizers, tests) and uploaded.
    ///
    /// PIXEL LAYOUT (the one convention every producer and consumer uses): tightly packed
    /// RGBA32, 4 bytes per pixel, rows BOTTOM-UP — row 0 is the bottom row of the image,
    /// exactly the original engine's <c>GetPixels32</c>/<c>GetRawTextureData</c> order.
    /// That is also what <c>glTexImage2D</c> wants for UV v = 0 at the bottom, and it makes
    /// sprite rects (pixel units, origin bottom-left) index the buffer directly.
    ///
    /// A texture built with the size-only constructor has no pixels (<see cref="HasPixels"/>
    /// is false) — headless code that only needs dimensions keeps working unchanged.
    /// </summary>
    public partial class Texture2D : Texture
    {
        byte[] m_Data;

        public Texture2D(int width, int height)
        {
            this.width = width;
            this.height = height;
        }

        /// <summary>Texture over RGBA32 pixels (bottom-up rows); the array is adopted, not copied.</summary>
        public Texture2D(int width, int height, byte[] rgba32BottomUp, bool mipChain = false)
        {
            if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (rgba32BottomUp == null) throw new ArgumentNullException(nameof(rgba32BottomUp));
            if (rgba32BottomUp.Length != width * height * 4)
                throw new ArgumentException($"expected {width * height * 4} bytes for {width}x{height} RGBA32, got {rgba32BottomUp.Length}");
            this.width = width;
            this.height = height;
            m_Data = rgba32BottomUp;
            if (mipChain) mipmapCount = 1 + (int)Math.Floor(Math.Log2(Math.Max(width, height)));
        }

        /// <summary>True when this texture carries pixel data.</summary>
        public bool HasPixels => m_Data != null;

        /// <summary>
        /// The texture's backing RGBA32 buffer (bottom-up rows). Returned by reference — the
        /// port never mutates it behind the texture's back; callers must not either.
        /// Null for a size-only texture.
        /// </summary>
        public byte[] GetRawTextureData() => m_Data;

        /// <summary>Replaces the pixel buffer (RGBA32, bottom-up, width*height*4 bytes).</summary>
        public void LoadRawTextureData(byte[] rgba32BottomUp)
        {
            if (rgba32BottomUp == null || rgba32BottomUp.Length != width * height * 4)
                throw new ArgumentException($"expected {width * height * 4} bytes for {width}x{height} RGBA32");
            m_Data = rgba32BottomUp;
        }

        /// <summary>Original contract: a copy of every pixel, bottom row first.</summary>
        public Color32[] GetPixels32()
        {
            var result = new Color32[width * height];
            if (m_Data == null) return result;
            for (int i = 0; i < result.Length; i++)
                result[i] = new Color32(m_Data[i * 4], m_Data[i * 4 + 1], m_Data[i * 4 + 2], m_Data[i * 4 + 3]);
            return result;
        }

        /// <summary>Original contract: writes every pixel (bottom row first) into the buffer.</summary>
        public void SetPixels32(Color32[] colors)
        {
            if (colors == null || colors.Length != width * height)
                throw new ArgumentException($"expected {width * height} colors");
            m_Data ??= new byte[width * height * 4];
            for (int i = 0; i < colors.Length; i++)
            {
                m_Data[i * 4] = colors[i].r;
                m_Data[i * 4 + 1] = colors[i].g;
                m_Data[i * 4 + 2] = colors[i].b;
                m_Data[i * 4 + 3] = colors[i].a;
            }
        }

        /// <summary>The pixel at (x, y), origin bottom-left, clamped to the texture. Clear when there is no data.</summary>
        public Color32 GetPixel32(int x, int y)
        {
            if (m_Data == null) return default;
            x = Math.Clamp(x, 0, width - 1);
            y = Math.Clamp(y, 0, height - 1);
            int i = (y * width + x) * 4;
            return new Color32(m_Data[i], m_Data[i + 1], m_Data[i + 2], m_Data[i + 3]);
        }

        /// <summary>Original contract: <see cref="GetPixel32"/> as a float colour.</summary>
        public Color GetPixel(int x, int y) => GetPixel32(x, y);

        /// <summary>No-op (original contract: uploads pending CPU edits to the GPU; the port uploads lazily).</summary>
        public void Apply(bool updateMipmaps = true, bool makeNoLongerReadable = false) => updateCount++;

        static Texture2D s_White;

        /// <summary>Original contract: a small opaque white texture (4x4), point-filtered, clamped.</summary>
        public static Texture2D whiteTexture
        {
            get
            {
                if (s_White != null) return s_White;
                var data = new byte[4 * 4 * 4];
                Array.Fill(data, (byte)255);
                s_White = new Texture2D(4, 4, data)
                {
                    name = "UnityWhite",
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                };
                return s_White;
            }
        }
    }
}
