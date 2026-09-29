using System.Collections.Generic;

namespace CosmicShore.Engine.UI
{
    /// <summary>Where layout looks up the default font, global fallbacks and the missing-glyph character.</summary>
    public interface ITmpFontResolver
    {
        TMP_FontAsset DefaultFontAsset { get; }
        IReadOnlyList<TMP_FontAsset> GlobalFallbackFontAssets { get; }
        /// <summary>0 = TMP's built-in U+25A1 (□).</summary>
        int MissingGlyphCharacter { get; }
    }

    /// <summary>Resolver backed by the engine-wide <see cref="TMP_Settings"/>.</summary>
    public sealed class TmpSettingsFontResolver : ITmpFontResolver
    {
        public static readonly TmpSettingsFontResolver Instance = new();
        public TMP_FontAsset DefaultFontAsset => TMP_Settings.defaultFontAsset;
        public IReadOnlyList<TMP_FontAsset> GlobalFallbackFontAssets => TMP_Settings.fallbackFontAssets ?? new List<TMP_FontAsset>();
        public int MissingGlyphCharacter => TMP_Settings.missingGlyphCharacter;
    }

    /// <summary>
    /// One TMP mesh vertex. <see cref="scale"/> is TMP's per-vertex SDF scale term (the
    /// glyph's element scale — canvas units per atlas texel); it is NEGATIVE for bold, which
    /// is how the SDF shader picks <c>_WeightBold</c> over <c>_WeightNormal</c>.
    /// </summary>
    public struct TmpVertex
    {
        public Vector3 position;
        /// <summary>Atlas UV, v measured from the bottom of the atlas (Unity convention).</summary>
        public Vector2 uv;
        public Color32 color;
        public float scale;

        public TmpVertex(Vector3 position, Vector2 uv, Color32 color, float scale)
        { this.position = position; this.uv = uv; this.color = color; this.scale = scale; }
    }

    public enum TmpQuadKind { Glyph = 0, Underline = 1, Strikethrough = 2, Highlight = 3 }

    /// <summary>
    /// One textured quad of laid-out text. Vertex order follows TMP: bottom-left, top-left,
    /// top-right, bottom-right. Positions are in the text's RectTransform local space (y up).
    /// </summary>
    public sealed class TmpQuad
    {
        public TmpQuadKind kind;
        public TMP_FontAsset font;
        public Material material;
        public int atlasIndex;
        public int characterIndex = -1;
        public TmpVertex bl, tl, tr, br;
    }

    /// <summary>Per-character layout result (TMP_CharacterInfo subset).</summary>
    public struct TmpCharacterInfo
    {
        public uint character;
        public int sourceIndex;
        public int lineNumber;
        public bool isVisible;
        public TMP_FontAsset font;
        public Glyph glyph;
        public float pointSize;
        public float scale;
        /// <summary>Pen position before / after the character, relative to the line start (pre-alignment).</summary>
        public float origin;
        public float xAdvance;
        public float baseLine;
        public float ascender;
        public float descender;
        /// <summary>Final glyph ink bounds (no SDF padding), rect-local.</summary>
        public Vector2 bottomLeft;
        public Vector2 topRight;
        public Color32 color;
        public FontStyles style;
    }

    /// <summary>Per-line layout result (TMP_LineInfo subset).</summary>
    public struct TmpLineInfo
    {
        public int firstCharacterIndex;
        public int lastCharacterIndex;
        public int lastVisibleCharacterIndex;
        public int characterCount;
        public int visibleCharacterCount;
        public int spaceCount;
        /// <summary>Pen advance after the last visible character (trailing whitespace and char spacing excluded).</summary>
        public float maxAdvance;
        public float width;
        public float ascender;
        public float descender;
        public float baseline;
        public float lineOffset;
        public bool endsWithLineFeed;
        public HorizontalAlignmentOptions alignment;
        public float alignmentOffset;
    }

    /// <summary>The full laid-out text: quads to draw plus TMP-style text info.</summary>
    public sealed class TmpLayoutResult
    {
        public readonly List<TmpQuad> quads = new();
        public readonly List<TmpCharacterInfo> characters = new();
        public readonly List<TmpLineInfo> lines = new();
        /// <summary>The rect the text was laid out in (RectTransform local space).</summary>
        public Rect rect;
        /// <summary>Final font size (after auto-size).</summary>
        public float fontSize;
        /// <summary>Final character width adjustment in [0, 1) (auto-size only).</summary>
        public float characterWidthAdjustment;
        public int autoSizeIterations;
        public bool overflowed;
        public bool isMasked;
        /// <summary>Clip rect for <see cref="TextOverflowModes.Masking"/> (equals <see cref="rect"/>).</summary>
        public Rect clipRect;
        /// <summary>Union of visible glyph ink bounds (rect-local).</summary>
        public Rect textBounds;
        public float preferredWidth;
        public float preferredHeight;
        /// <summary>Material used for the base text (per-quad materials may differ).</summary>
        public Material material;
    }
}
