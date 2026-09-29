using System;
using System.Collections.Generic;

namespace CosmicShore.Engine.UI
{
    /// <summary>
    /// TextMeshPro alignment (original TMPro numeric values: one horizontal bit 1..32 OR'd
    /// with one vertical bit 256..8192). Every combination TMP defines is present so a
    /// serialized <c>m_textAlignment</c> round-trips; <see cref="Converted"/> (0xFFFF) is the
    /// marker newer TMP writes once the value has been split into
    /// <c>m_HorizontalAlignment</c> / <c>m_VerticalAlignment</c>.
    /// </summary>
    public enum TextAlignmentOptions
    {
        TopLeft = 257,
        Top = 258,
        TopRight = 260,
        TopJustified = 264,
        TopFlush = 272,
        TopGeoAligned = 288,

        Left = 513,
        Center = 514,
        Right = 516,
        Justified = 520,
        Flush = 528,
        CenterGeoAligned = 544,

        BottomLeft = 1025,
        Bottom = 1026,
        BottomRight = 1028,
        BottomJustified = 1032,
        BottomFlush = 1040,
        BottomGeoAligned = 1056,

        BaselineLeft = 2049,
        Baseline = 2050,
        BaselineRight = 2052,
        BaselineJustified = 2056,
        BaselineFlush = 2064,
        BaselineGeoAligned = 2080,

        MidlineLeft = 4097,
        Midline = 4098,
        MidlineRight = 4100,
        MidlineJustified = 4104,
        MidlineFlush = 4112,
        MidlineGeoAligned = 4128,

        CaplineLeft = 8193,
        Capline = 8194,
        CaplineRight = 8196,
        CaplineJustified = 8200,
        CaplineFlush = 8208,
        CaplineGeoAligned = 8224,

        Converted = 65535,
    }

    /// <summary>TMP horizontal alignment (serialized <c>m_HorizontalAlignment</c>).</summary>
    public enum HorizontalAlignmentOptions
    {
        Left = 1,
        Center = 2,
        Right = 4,
        Justified = 8,
        Flush = 16,
        Geometry = 32,
    }

    /// <summary>TMP vertical alignment (serialized <c>m_VerticalAlignment</c>). "Geometry" is TMP's Midline.</summary>
    public enum VerticalAlignmentOptions
    {
        Top = 256,
        Middle = 512,
        Bottom = 1024,
        Baseline = 2048,
        Geometry = 4096,
        Capline = 8192,
    }

    /// <summary>TMP font style flags (serialized <c>m_fontStyle</c>).</summary>
    [Flags]
    public enum FontStyles
    {
        Normal = 0,
        Bold = 1,
        Italic = 2,
        Underline = 4,
        LowerCase = 8,
        UpperCase = 16,
        SmallCaps = 32,
        Strikethrough = 64,
        Superscript = 128,
        Subscript = 256,
        Highlight = 512,
    }

    /// <summary>TMP overflow modes (serialized <c>m_overflowMode</c>).</summary>
    public enum TextOverflowModes
    {
        Overflow = 0,
        Ellipsis = 1,
        Masking = 2,
        Truncate = 3,
        ScrollRect = 4,
        Page = 5,
        Linked = 6,
    }

    /// <summary>TMP text wrapping modes (serialized <c>m_TextWrappingMode</c>).</summary>
    public enum TextWrappingModes
    {
        NoWrap = 0,
        Normal = 1,
        PreserveWhitespace = 2,
        PreserveWhitespaceNoWrap = 3,
    }

    /// <summary>TMP vertex-color mode for <see cref="VertexGradient"/> (serialized <c>m_colorMode</c>).</summary>
    public enum ColorMode
    {
        Single = 0,
        HorizontalGradient = 1,
        VerticalGradient = 2,
        FourCornersGradient = 3,
    }

    /// <summary>Four-corner vertex color gradient (serialized <c>m_fontColorGradient</c>).</summary>
    [Serializable]
    public struct VertexGradient
    {
        public Color topLeft;
        public Color topRight;
        public Color bottomLeft;
        public Color bottomRight;

        public VertexGradient(Color color) { topLeft = topRight = bottomLeft = bottomRight = color; }
        public VertexGradient(Color tl, Color tr, Color bl, Color br) { topLeft = tl; topRight = tr; bottomLeft = bl; bottomRight = br; }
    }

    // ── Font asset data (UnityEngine.TextCore shapes, TMP field names) ────────────────────

    /// <summary>Font face metrics, in font units at <see cref="pointSize"/> (== atlas pixels).</summary>
    [Serializable]
    public struct FaceInfo
    {
        public int faceIndex;
        public string familyName;
        public string styleName;
        public float pointSize;
        public float scale;
        public int unitsPerEM;
        public float lineHeight;
        public float ascentLine;
        public float capLine;
        public float meanLine;
        public float baseline;
        public float descentLine;
        public float superscriptOffset;
        public float superscriptSize;
        public float subscriptOffset;
        public float subscriptSize;
        public float underlineOffset;
        public float underlineThickness;
        public float strikethroughOffset;
        public float strikethroughThickness;
        public float tabWidth;
    }

    /// <summary>Glyph metrics in font units at the face point size.</summary>
    [Serializable]
    public struct GlyphMetrics
    {
        public float width;
        public float height;
        public float horizontalBearingX;
        public float horizontalBearingY;
        public float horizontalAdvance;

        public GlyphMetrics(float width, float height, float bearingX, float bearingY, float advance)
        {
            this.width = width; this.height = height;
            horizontalBearingX = bearingX; horizontalBearingY = bearingY; horizontalAdvance = advance;
        }
    }

    /// <summary>A glyph's rectangle on its atlas, in pixels, origin BOTTOM-left (Unity texture convention).</summary>
    [Serializable]
    public struct GlyphRect
    {
        public int x;
        public int y;
        public int width;
        public int height;

        public GlyphRect(int x, int y, int width, int height) { this.x = x; this.y = y; this.width = width; this.height = height; }
    }

    /// <summary>One glyph of a font asset's glyph table.</summary>
    public sealed class Glyph
    {
        public uint index;
        public GlyphMetrics metrics;
        public GlyphRect glyphRect;
        public float scale = 1f;
        public int atlasIndex;
    }

    /// <summary>One entry of a font asset's character table (unicode → glyph).</summary>
    public sealed class TMP_Character
    {
        public uint unicode;
        public uint glyphIndex;
        public float scale = 1f;
        public Glyph glyph;
    }

    /// <summary>OpenType GPOS value record (font units at point size).</summary>
    [Serializable]
    public struct GlyphValueRecord
    {
        public float xPlacement;
        public float yPlacement;
        public float xAdvance;
        public float yAdvance;
    }

    /// <summary>A kerning pair: adjustments applied to the first and second glyph.</summary>
    public sealed class GlyphPairAdjustmentRecord
    {
        public uint firstGlyphIndex;
        public GlyphValueRecord firstAdjustment;
        public uint secondGlyphIndex;
        public GlyphValueRecord secondAdjustment;
    }

    /// <summary>
    /// TMP_FontAsset: face metrics, glyph + character tables, kerning pairs, the SDF atlas
    /// and the font's default material. Loaded from the real Unity asset by
    /// <c>CosmicShore.Content.Fonts.TmpFontLibrary</c>.
    /// <para>PORT NOTE: <see cref="Texture2D"/> does not hold pixels yet (textures are a
    /// separate work item), so the atlas pixels live here in <see cref="atlasPixels"/>:
    /// one Alpha8 byte array per atlas texture, <c>atlasWidth × atlasHeight</c>, rows
    /// BOTTOM-UP exactly as Unity serializes them — so <c>v = glyphRect.y / atlasHeight</c>
    /// addresses row <c>glyphRect.y</c> directly.</para>
    /// </summary>
    public class TMP_FontAsset : ScriptableObject
    {
        public FaceInfo faceInfo;
        public readonly List<Glyph> glyphTable = new();
        public readonly List<TMP_Character> characterTable = new();
        public readonly Dictionary<uint, Glyph> glyphLookupTable = new();
        public readonly Dictionary<uint, TMP_Character> characterLookupTable = new();
        public readonly Dictionary<ulong, GlyphPairAdjustmentRecord> glyphPairAdjustmentLookup = new();
        public readonly List<TMP_FontAsset> fallbackFontAssetTable = new();

        public int atlasWidth;
        public int atlasHeight;
        public int atlasPadding;
        /// <summary>UnityEngine.TextCore GlyphRenderMode (4165 = SDFAA, 4169 = SDFAA_HINTED, …).</summary>
        public int atlasRenderMode;
        /// <summary>0 = Static, 1 = Dynamic, 2 = DynamicOS.</summary>
        public int atlasPopulationMode;
        public string sourceFontFileGuid;
        public Texture2D[] atlasTextures = Array.Empty<Texture2D>();
        /// <summary>Alpha8 atlas pixels, one array per atlas texture (see class remarks).</summary>
        public byte[][] atlasPixels = Array.Empty<byte[]>();

        /// <summary>The font's default (serialized) material.</summary>
        public Material material;

        public float normalStyle;
        public float normalSpacingOffset;
        public float boldStyle = 0.75f;
        public float boldSpacing = 7f;
        public byte italicStyle = 35;
        public byte tabSize = 10;

        public Texture2D atlasTexture => atlasTextures.Length > 0 ? atlasTextures[0] : null;

        /// <summary>Rebuilds the lookup tables from the glyph/character/kerning lists.</summary>
        public void ReadFontAssetDefinition()
        {
            glyphLookupTable.Clear();
            foreach (var g in glyphTable) glyphLookupTable[g.index] = g;
            characterLookupTable.Clear();
            foreach (var c in characterTable)
            {
                if (glyphLookupTable.TryGetValue(c.glyphIndex, out var g)) c.glyph = g;
                if (c.glyph != null) characterLookupTable[c.unicode] = c;
            }
        }

        public void AddPairAdjustment(GlyphPairAdjustmentRecord record)
            => glyphPairAdjustmentLookup[PairKey(record.firstGlyphIndex, record.secondGlyphIndex)] = record;

        public static ulong PairKey(uint first, uint second) => ((ulong)second << 32) | first;

        public bool HasCharacter(uint unicode) => characterLookupTable.ContainsKey(unicode);

        public bool TryGetCharacter(uint unicode, out TMP_Character character)
            => characterLookupTable.TryGetValue(unicode, out character);

        public bool TryGetPairAdjustment(uint first, uint second, out GlyphPairAdjustmentRecord record)
            => glyphPairAdjustmentLookup.TryGetValue(PairKey(first, second), out record);

        /// <summary>Samples an atlas texel (row-major, bottom-up), clamped; returns [0,1].</summary>
        public float SampleAtlas(int atlasIndex, int x, int y)
        {
            if ((uint)atlasIndex >= (uint)atlasPixels.Length) return 0f;
            var px = atlasPixels[atlasIndex];
            if (px == null || px.Length == 0) return 0f;
            x = x < 0 ? 0 : (x >= atlasWidth ? atlasWidth - 1 : x);
            y = y < 0 ? 0 : (y >= atlasHeight ? atlasHeight - 1 : y);
            return px[y * atlasWidth + x] * (1f / 255f);
        }

        /// <summary>Bilinear atlas sample at UV (v measured from the bottom), like a filtered GPU fetch.</summary>
        public float SampleAtlasBilinear(int atlasIndex, float u, float v)
        {
            float fx = u * atlasWidth - 0.5f, fy = v * atlasHeight - 0.5f;
            int x0 = (int)MathF.Floor(fx), y0 = (int)MathF.Floor(fy);
            float tx = fx - x0, ty = fy - y0;
            float a = SampleAtlas(atlasIndex, x0, y0), b = SampleAtlas(atlasIndex, x0 + 1, y0);
            float c = SampleAtlas(atlasIndex, x0, y0 + 1), d = SampleAtlas(atlasIndex, x0 + 1, y0 + 1);
            return (a + (b - a) * tx) + ((c + (d - c) * tx) - (a + (b - a) * tx)) * ty;
        }
    }

    /// <summary>Project-wide TMP settings surface (original: TMP_Settings asset statics).</summary>
    public static class TMP_Settings
    {
        public static TMP_FontAsset defaultFontAsset { get; set; }
        /// <summary>Global fallback font list (TMP Settings <c>m_fallbackFontAssets</c>).</summary>
        public static List<TMP_FontAsset> fallbackFontAssets { get; set; } = new();
        /// <summary>0 means TMP's built-in missing glyph U+25A1 (□).</summary>
        public static int missingGlyphCharacter { get; set; }
        public static float defaultFontSize { get; set; } = 36f;
        public static bool enableKerning { get; set; } = true;
    }

    /// <summary>
    /// The TMPro shim. Holds the serialized state Unity's TMP_Text / TextMeshProUGUI write
    /// into scenes under their real field names (<c>m_text</c>, <c>m_fontSize</c>,
    /// <c>m_HorizontalAlignment</c>, …) so a scene loader can fill them by name, plus the
    /// public property surface ported code uses (<see cref="text"/>, <see cref="color"/>,
    /// <see cref="fontSize"/>, <see cref="alignment"/>, <see cref="font"/>). Layout is
    /// <see cref="TmpLayout"/>; rendering is <see cref="TmpSdfShader"/> /
    /// <see cref="TmpSoftwareRaster"/>. Substitution: `using TMPro;` → `using CosmicShore.Engine.UI;`.
    /// </summary>
    public abstract class TMP_Text : Behaviour
    {
        /// <summary>The 'kern' OpenType feature tag as TMP serializes it (little-endian "kern").</summary>
        public const uint KernFeatureTag = 0x6E72656B;

        [SerializeField] protected string m_text = string.Empty;
        [SerializeField] protected bool m_isRightToLeft;
        [SerializeField] protected TMP_FontAsset m_fontAsset;
        [SerializeField] protected Material m_sharedMaterial;
        [SerializeField] protected Material m_fontMaterial;
        [SerializeField] protected Material m_baseMaterial;
        [SerializeField] protected Color32 m_fontColor32 = new(255, 255, 255, 255);
        [SerializeField] protected Color m_fontColor = Color.white;
        [SerializeField] protected Color32 m_faceColor = new(255, 255, 255, 255);
        [SerializeField] protected bool m_enableVertexGradient;
        [SerializeField] protected ColorMode m_colorMode = ColorMode.FourCornersGradient;
        [SerializeField] protected VertexGradient m_fontColorGradient = new(Color.white);
        [SerializeField] protected bool m_tintAllSprites;
        [SerializeField] protected bool m_overrideHtmlColors;
        [SerializeField] protected float m_fontSize = 36f;
        [SerializeField] protected float m_fontSizeBase = 36f;
        [SerializeField] protected int m_fontWeight = 400;
        [SerializeField] protected bool m_enableAutoSizing;
        [SerializeField] protected float m_fontSizeMin = 18f;
        [SerializeField] protected float m_fontSizeMax = 72f;
        [SerializeField] protected FontStyles m_fontStyle = FontStyles.Normal;
        [SerializeField] protected HorizontalAlignmentOptions m_HorizontalAlignment = HorizontalAlignmentOptions.Left;
        [SerializeField] protected VerticalAlignmentOptions m_VerticalAlignment = VerticalAlignmentOptions.Top;
        [SerializeField] protected TextAlignmentOptions m_textAlignment = TextAlignmentOptions.Converted;
        [SerializeField] protected float m_characterSpacing;
        [SerializeField] protected float m_characterHorizontalScale = 1f;
        [SerializeField] protected float m_wordSpacing;
        [SerializeField] protected float m_lineSpacing;
        [SerializeField] protected float m_lineSpacingMax;
        [SerializeField] protected float m_paragraphSpacing;
        [SerializeField] protected float m_charWidthMaxAdj;
        [SerializeField] protected TextWrappingModes m_TextWrappingMode = TextWrappingModes.Normal;
        /// <summary>Legacy (pre-TextWrappingModes) serialized flag; honoured when set by an old asset.</summary>
        [SerializeField] protected bool m_enableWordWrapping = true;
        [SerializeField] protected float m_wordWrappingRatios = 0.4f;
        [SerializeField] protected TextOverflowModes m_overflowMode = TextOverflowModes.Overflow;
        [SerializeField] protected bool m_enableKerning = true;
        /// <summary>OpenType feature tags (serialized as a hex blob, e.g. <c>6e72656b</c> = 'kern').</summary>
        [SerializeField] protected List<uint> m_ActiveFontFeatures = new() { KernFeatureTag };
        [SerializeField] protected bool m_enableExtraPadding;
        [SerializeField] protected bool m_isRichText = true;
        [SerializeField] protected bool m_parseCtrlCharacters = true;
        [SerializeField] protected bool m_isOrthographic = true;
        [SerializeField] protected bool m_isCullingEnabled;
        [SerializeField] protected bool m_useMaxVisibleDescender = true;
        [SerializeField] protected int m_pageToDisplay = 1;
        /// <summary>Margins: x = left, y = top, z = right, w = bottom (canvas units).</summary>
        [SerializeField] protected Vector4 m_margin = Vector4.zero;
        [SerializeField] protected bool m_isVolumetricText;
        [SerializeField] protected int m_maxVisibleCharacters = 99999;
        [SerializeField] protected int m_maxVisibleWords = 99999;
        [SerializeField] protected int m_maxVisibleLines = 99999;

        // ── public surface (TMP property names) ───────────────────────────────────────

        public string text
        {
            get => m_text;
            set => m_text = value ?? string.Empty;
        }

        /// <summary>Font color (TMP overrides Graphic.color to read/write m_fontColor).</summary>
        public Color color
        {
            get => m_fontColor;
            set { m_fontColor = value; m_fontColor32 = value; }
        }

        public float fontSize
        {
            get => m_fontSize;
            set { m_fontSize = value; if (!m_enableAutoSizing) m_fontSizeBase = value; }
        }

        public float fontSizeBase => m_fontSizeBase;

        public TextAlignmentOptions alignment
        {
            get => (TextAlignmentOptions)((int)m_HorizontalAlignment | (int)m_VerticalAlignment);
            set
            {
                m_HorizontalAlignment = (HorizontalAlignmentOptions)((int)value & 0xFF);
                m_VerticalAlignment = (VerticalAlignmentOptions)((int)value & 0xFF00);
                m_textAlignment = TextAlignmentOptions.Converted;
            }
        }

        public HorizontalAlignmentOptions horizontalAlignment { get => m_HorizontalAlignment; set => m_HorizontalAlignment = value; }
        public VerticalAlignmentOptions verticalAlignment { get => m_VerticalAlignment; set => m_VerticalAlignment = value; }

        public TMP_FontAsset font
        {
            get => m_fontAsset;
            set => m_fontAsset = value;
        }

        /// <summary>The material the text renders with (falls back to the font's default material).</summary>
        public Material fontSharedMaterial
        {
            get => m_sharedMaterial ?? m_fontAsset?.material;
            set => m_sharedMaterial = value;
        }

        public Color32 faceColor { get => m_faceColor; set => m_faceColor = value; }
        public bool enableVertexGradient { get => m_enableVertexGradient; set => m_enableVertexGradient = value; }
        public ColorMode colorMode { get => m_colorMode; set => m_colorMode = value; }
        public VertexGradient colorGradient { get => m_fontColorGradient; set => m_fontColorGradient = value; }
        public bool overrideColorTags { get => m_overrideHtmlColors; set => m_overrideHtmlColors = value; }
        public FontStyles fontStyle { get => m_fontStyle; set => m_fontStyle = value; }
        public int fontWeight { get => m_fontWeight; set => m_fontWeight = value; }
        public bool enableAutoSizing { get => m_enableAutoSizing; set => m_enableAutoSizing = value; }
        public float fontSizeMin { get => m_fontSizeMin; set => m_fontSizeMin = value; }
        public float fontSizeMax { get => m_fontSizeMax; set => m_fontSizeMax = value; }
        public float characterSpacing { get => m_characterSpacing; set => m_characterSpacing = value; }
        public float characterHorizontalScale { get => m_characterHorizontalScale; set => m_characterHorizontalScale = value; }
        public float wordSpacing { get => m_wordSpacing; set => m_wordSpacing = value; }
        public float lineSpacing { get => m_lineSpacing; set => m_lineSpacing = value; }
        public float lineSpacingAdjustment { get => m_lineSpacingMax; set => m_lineSpacingMax = value; }
        public float paragraphSpacing { get => m_paragraphSpacing; set => m_paragraphSpacing = value; }
        public float characterWidthAdjustment { get => m_charWidthMaxAdj; set => m_charWidthMaxAdj = value; }
        public float wordWrappingRatios { get => m_wordWrappingRatios; set => m_wordWrappingRatios = value; }
        public TextOverflowModes overflowMode { get => m_overflowMode; set => m_overflowMode = value; }
        public Vector4 margin { get => m_margin; set => m_margin = value; }
        public bool richText { get => m_isRichText; set => m_isRichText = value; }
        public bool parseCtrlCharacters { get => m_parseCtrlCharacters; set => m_parseCtrlCharacters = value; }
        public bool isOrthographic { get => m_isOrthographic; set => m_isOrthographic = value; }
        public bool isRightToLeftText { get => m_isRightToLeft; set => m_isRightToLeft = value; }
        public bool extraPadding { get => m_enableExtraPadding; set => m_enableExtraPadding = value; }
        public int maxVisibleCharacters { get => m_maxVisibleCharacters; set => m_maxVisibleCharacters = value; }
        public int maxVisibleWords { get => m_maxVisibleWords; set => m_maxVisibleWords = value; }
        public int maxVisibleLines { get => m_maxVisibleLines; set => m_maxVisibleLines = value; }

        public TextWrappingModes textWrappingMode
        {
            get => m_TextWrappingMode;
            set { m_TextWrappingMode = value; m_enableWordWrapping = value == TextWrappingModes.Normal || value == TextWrappingModes.PreserveWhitespace; }
        }

        /// <summary>Legacy TMP toggle; mapped onto <see cref="textWrappingMode"/>.</summary>
        public bool enableWordWrapping
        {
            get => m_TextWrappingMode == TextWrappingModes.Normal || m_TextWrappingMode == TextWrappingModes.PreserveWhitespace;
            set => textWrappingMode = value ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
        }

        /// <summary>Kerning is active when the 'kern' feature is enabled (TMP 3.2+ semantics).</summary>
        public bool enableKerning
        {
            get => m_ActiveFontFeatures != null ? m_ActiveFontFeatures.Contains(KernFeatureTag) : m_enableKerning;
            set
            {
                m_enableKerning = value;
                m_ActiveFontFeatures ??= new List<uint>();
                m_ActiveFontFeatures.Remove(KernFeatureTag);
                if (value) m_ActiveFontFeatures.Add(KernFeatureTag);
            }
        }

        /// <summary>Raw feature list (null = legacy asset: fall back to m_enableKerning).</summary>
        public List<uint> activeFontFeatures { get => m_ActiveFontFeatures; set => m_ActiveFontFeatures = value; }

        /// <summary>
        /// Applies a legacy combined <c>m_textAlignment</c> value (assets serialized before TMP
        /// split it) onto the horizontal/vertical fields. The Converted marker is ignored.
        /// </summary>
        public void ApplyLegacyAlignment(int textAlignment)
        {
            if (textAlignment == (int)TextAlignmentOptions.Converted || textAlignment <= 0) return;
            alignment = (TextAlignmentOptions)textAlignment;
        }

        /// <summary>The margin rect-relative width consumed by layout (x = left, z = right).</summary>
        public float marginLeft => m_margin.x;
        public float marginTop => m_margin.y;
        public float marginRight => m_margin.z;
        public float marginBottom => m_margin.w;
    }

    /// <summary>World-space (3D) TextMeshPro component (non-orthographic font scale × 0.1).</summary>
    public class TextMeshPro : TMP_Text
    {
        public TextMeshPro() { m_isOrthographic = false; }
    }

    /// <summary>
    /// Data-only input-field shim (original contract: TMPro.TMP_InputField — the slice
    /// ported controllers read/write: the entered text plus interactability via Behaviour).
    /// A render/input layer binds it later; tests set <see cref="text"/> directly.
    /// </summary>
    public class TMP_InputField : Behaviour
    {
        public string text = string.Empty;
    }

    /// <summary>Canvas (UGUI) TextMeshPro component.</summary>
    public class TextMeshProUGUI : TMP_Text
    {
    }
}
