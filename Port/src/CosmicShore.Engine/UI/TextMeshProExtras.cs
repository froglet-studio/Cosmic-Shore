using System;
using System.Globalization;
using System.Text;

namespace CosmicShore.Engine.UI
{
    /// <summary>One laid-out character (original contract: TMPro.TMP_CharacterInfo, the fields ported code reads).</summary>
    public struct TMP_CharacterInfo
    {
        public char character;
        public int index;
        public int lineNumber;
        public bool isVisible;
        public TMP_FontAsset fontAsset;
        public float pointSize;
        public float scale;
        public float origin;
        public float xAdvance;
        public float baseLine;
        public float ascender;
        public float descender;
        public Vector3 bottomLeft;
        public Vector3 topLeft;
        public Vector3 topRight;
        public Vector3 bottomRight;
        public Color32 color;
        public FontStyles style;
        public int materialReferenceIndex;
        public int vertexIndex;
    }

    /// <summary>One laid-out line (original contract: TMPro.TMP_LineInfo).</summary>
    public struct TMP_LineInfo
    {
        public int characterCount;
        public int visibleCharacterCount;
        public int spaceCount;
        public int wordCount;
        public int firstCharacterIndex;
        public int firstVisibleCharacterIndex;
        public int lastCharacterIndex;
        public int lastVisibleCharacterIndex;
        public float length;
        public float lineHeight;
        public float ascender;
        public float baseline;
        public float descender;
        public float maxAdvance;
        public float width;
        public HorizontalAlignmentOptions alignment;
    }

    /// <summary>
    /// The result of the last text layout (original contract: TMPro.TMP_TextInfo).
    /// Filled by <see cref="TMP_Text.ForceMeshUpdate"/> — and lazily on first read after the
    /// text or its properties changed — from <see cref="TmpLayout"/>, the same layout the
    /// renderer draws, so counts always agree with what is on screen.
    /// </summary>
    public class TMP_TextInfo
    {
        public TMP_Text textComponent;
        public int characterCount;
        public int spriteCount;
        public int spaceCount;
        public int wordCount;
        public int linkCount;
        public int lineCount;
        public int pageCount = 1;
        public int materialCount = 1;
        public TMP_CharacterInfo[] characterInfo = Array.Empty<TMP_CharacterInfo>();
        public TMP_LineInfo[] lineInfo = Array.Empty<TMP_LineInfo>();

        public TMP_TextInfo() { }
        public TMP_TextInfo(TMP_Text textComponent) { this.textComponent = textComponent; }

        public void Clear()
        {
            characterCount = spriteCount = spaceCount = wordCount = linkCount = lineCount = 0;
        }
    }

    public abstract partial class TMP_Text : ILayoutElement
    {
        protected bool m_havePropertiesChanged = true;
        TMP_TextInfo m_textInfo;
        TmpLayoutResult m_lastLayout;
        Rect m_lastLayoutRect;

        /// <summary>True when the text or a layout-relevant property changed since the last layout.</summary>
        public bool havePropertiesChanged { get => m_havePropertiesChanged; set => m_havePropertiesChanged = value; }

        public TMP_TextInfo textInfo
        {
            get
            {
                EnsureLayout();
                return m_textInfo;
            }
        }

        /// <summary>Re-runs layout now so <see cref="textInfo"/> reflects the current text (original contract).</summary>
        public void ForceMeshUpdate(bool ignoreActiveState = false, bool forceTextReparsing = false)
        {
            m_havePropertiesChanged = true;
            EnsureLayout();
        }

        public void ForceMeshUpdate() => ForceMeshUpdate(false, false);

        public void UpdateVertexData() { }
        public void UpdateMeshPadding() { }
        public override void SetVerticesDirty() { m_havePropertiesChanged = true; base.SetVerticesDirty(); }
        public override void SetAllDirty() { m_havePropertiesChanged = true; base.SetAllDirty(); }

        Rect LayoutRect()
            => transform is RectTransform rt ? rt.rect : new Rect(0, 0, 20f, 5f);

        TmpLayoutResult EnsureLayout()
        {
            var rect = LayoutRect();
            if (!m_havePropertiesChanged && m_lastLayout != null && m_lastLayoutRect == rect) return m_lastLayout;
            m_textInfo ??= new TMP_TextInfo(this);
            TmpLayoutResult result = null;
            try { result = TmpLayout.Layout(this, rect); }
            catch (Exception) { result = null; }
            m_lastLayout = result;
            m_lastLayoutRect = rect;
            m_havePropertiesChanged = false;
            FillTextInfo(result);
            return result;
        }

        void FillTextInfo(TmpLayoutResult r)
        {
            var info = m_textInfo;
            info.Clear();
            if (r == null)
            {
                // No font available (headless without content): count the parsed characters.
                var parsed = GetParsedText();
                info.characterCount = parsed.Length;
                if (info.characterInfo.Length < parsed.Length) info.characterInfo = new TMP_CharacterInfo[parsed.Length];
                for (int i = 0; i < parsed.Length; i++)
                    info.characterInfo[i] = new TMP_CharacterInfo { character = parsed[i], index = i, isVisible = !char.IsWhiteSpace(parsed[i]) };
                info.lineCount = parsed.Length == 0 ? 0 : 1;
                return;
            }

            int n = r.characters.Count;
            if (info.characterInfo.Length < n) info.characterInfo = new TMP_CharacterInfo[Math.Max(n, 8)];
            bool inWord = false;
            for (int i = 0; i < n; i++)
            {
                var c = r.characters[i];
                char ch = c.character <= 0xFFFF ? (char)c.character : '�';
                float top = c.baseLine + c.ascender, bottom = c.baseLine + c.descender;
                info.characterInfo[i] = new TMP_CharacterInfo
                {
                    character = ch,
                    index = c.sourceIndex,
                    lineNumber = c.lineNumber,
                    isVisible = c.isVisible,
                    fontAsset = c.font,
                    pointSize = c.pointSize,
                    scale = c.scale,
                    origin = c.origin,
                    xAdvance = c.xAdvance,
                    baseLine = c.baseLine,
                    ascender = c.ascender,
                    descender = c.descender,
                    bottomLeft = new Vector3(c.bottomLeft.x, c.bottomLeft.y, 0f),
                    topRight = new Vector3(c.topRight.x, c.topRight.y, 0f),
                    topLeft = new Vector3(c.bottomLeft.x, c.topRight.y, 0f),
                    bottomRight = new Vector3(c.topRight.x, c.bottomLeft.y, 0f),
                    color = c.color,
                    style = c.style,
                    vertexIndex = i * 4,
                };
                if (char.IsWhiteSpace(ch)) { info.spaceCount++; inWord = false; }
                else if (!inWord) { info.wordCount++; inWord = true; }
            }
            info.characterCount = n;

            int lines = r.lines.Count;
            if (info.lineInfo.Length < lines) info.lineInfo = new TMP_LineInfo[Math.Max(lines, 2)];
            for (int i = 0; i < lines; i++)
            {
                var l = r.lines[i];
                info.lineInfo[i] = new TMP_LineInfo
                {
                    characterCount = l.characterCount,
                    visibleCharacterCount = l.visibleCharacterCount,
                    spaceCount = l.spaceCount,
                    firstCharacterIndex = l.firstCharacterIndex,
                    lastCharacterIndex = l.lastCharacterIndex,
                    lastVisibleCharacterIndex = l.lastVisibleCharacterIndex,
                    maxAdvance = l.maxAdvance,
                    width = l.width,
                    length = l.width,
                    ascender = l.ascender,
                    descender = l.descender,
                    baseline = l.baseline,
                    lineHeight = l.ascender - l.descender,
                    alignment = l.alignment,
                };
            }
            info.lineCount = lines;
        }

        /// <summary>The text with rich-text tags stripped (original contract).</summary>
        public string GetParsedText()
        {
            var t = m_text ?? string.Empty;
            if (!m_isRichText || t.IndexOf('<') < 0) return t;
            var sb = new StringBuilder(t.Length);
            for (int i = 0; i < t.Length; i++)
            {
                if (t[i] == '<')
                {
                    int close = t.IndexOf('>', i + 1);
                    if (close > i) { i = close; continue; }
                }
                sb.Append(t[i]);
            }
            return sb.ToString();
        }

        // ── SetText (original contract: format tokens {0}, {0:2} = two decimals) ──

        public void SetText(string sourceText) => text = sourceText;
        public void SetText(string sourceText, bool syncTextInputBox) => text = sourceText;
        public void SetText(string sourceText, float arg0) => text = Format(sourceText, arg0, 0, 0, 0, 0, 0, 0, 0);
        public void SetText(string sourceText, float arg0, float arg1) => text = Format(sourceText, arg0, arg1, 0, 0, 0, 0, 0, 0);
        public void SetText(string sourceText, float arg0, float arg1, float arg2) => text = Format(sourceText, arg0, arg1, arg2, 0, 0, 0, 0, 0);
        public void SetText(string sourceText, float arg0, float arg1, float arg2, float arg3) => text = Format(sourceText, arg0, arg1, arg2, arg3, 0, 0, 0, 0);
        public void SetText(string sourceText, float arg0, float arg1, float arg2, float arg3, float arg4) => text = Format(sourceText, arg0, arg1, arg2, arg3, arg4, 0, 0, 0);
        public void SetText(string sourceText, float arg0, float arg1, float arg2, float arg3, float arg4, float arg5) => text = Format(sourceText, arg0, arg1, arg2, arg3, arg4, arg5, 0, 0);
        public void SetText(string sourceText, float arg0, float arg1, float arg2, float arg3, float arg4, float arg5, float arg6) => text = Format(sourceText, arg0, arg1, arg2, arg3, arg4, arg5, arg6, 0);
        public void SetText(string sourceText, float arg0, float arg1, float arg2, float arg3, float arg4, float arg5, float arg6, float arg7) => text = Format(sourceText, arg0, arg1, arg2, arg3, arg4, arg5, arg6, arg7);
        public void SetText(StringBuilder sourceText) => text = sourceText?.ToString() ?? string.Empty;
        public void SetText(char[] sourceText) => text = sourceText == null ? string.Empty : new string(sourceText);
        public void SetText(char[] sourceText, int start, int length) => text = sourceText == null ? string.Empty : new string(sourceText, start, length);
        public void SetCharArray(char[] sourceText) => SetText(sourceText);
        public void SetCharArray(char[] sourceText, int start, int length) => SetText(sourceText, start, length);

        static string Format(string src, float a0, float a1, float a2, float a3, float a4, float a5, float a6, float a7)
        {
            if (string.IsNullOrEmpty(src)) return string.Empty;
            var args = new[] { a0, a1, a2, a3, a4, a5, a6, a7 };
            var sb = new StringBuilder(src.Length + 16);
            for (int i = 0; i < src.Length; i++)
            {
                char c = src[i];
                if (c == '{')
                {
                    int close = src.IndexOf('}', i + 1);
                    if (close > i + 1)
                    {
                        var token = src.AsSpan(i + 1, close - i - 1);
                        int colon = token.IndexOf(':');
                        var idxSpan = colon >= 0 ? token[..colon] : token;
                        if (int.TryParse(idxSpan, NumberStyles.Integer, CultureInfo.InvariantCulture, out int idx) && idx >= 0 && idx < 8)
                        {
                            int decimals = 0;
                            bool fixedDecimals = colon >= 0 &&
                                int.TryParse(token[(colon + 1)..], NumberStyles.Integer, CultureInfo.InvariantCulture, out decimals);
                            float v = args[idx];
                            sb.Append(fixedDecimals
                                ? v.ToString("F" + Math.Clamp(decimals, 0, 9), CultureInfo.InvariantCulture)
                                : ((double)v).ToString("0.#####", CultureInfo.InvariantCulture));
                            i = close;
                            continue;
                        }
                    }
                }
                sb.Append(c);
            }
            return sb.ToString();
        }

        // ── sizes (ILayoutElement: TMP reports its text's preferred size) ──

        public float renderedWidth => EnsureLayout()?.textBounds.width ?? 0f;
        public float renderedHeight => EnsureLayout()?.textBounds.height ?? 0f;
        public Bounds textBounds
        {
            get
            {
                var b = EnsureLayout()?.textBounds ?? default;
                return new Bounds(new Vector3(b.center.x, b.center.y, 0f), new Vector3(b.width, b.height, 0f));
            }
        }
        public bool isTextOverflowing => EnsureLayout()?.overflowed ?? false;

        public Vector2 GetPreferredValues() { var r = EnsureLayout(); return r == null ? Vector2.zero : new Vector2(r.preferredWidth, r.preferredHeight); }
        public Vector2 GetPreferredValues(string text)
        {
            string saved = m_text;
            m_text = text ?? string.Empty;
            m_havePropertiesChanged = true;
            var v = GetPreferredValues();
            m_text = saved;
            m_havePropertiesChanged = true;
            return v;
        }
        public Vector2 GetPreferredValues(float width, float height) => GetPreferredValues();
        public Vector2 GetRenderedValues() => new(renderedWidth, renderedHeight);
        public Vector2 GetRenderedValues(bool onlyVisibleCharacters) => GetRenderedValues();

        public virtual void CalculateLayoutInputHorizontal() { }
        public virtual void CalculateLayoutInputVertical() { }
        public virtual float minWidth => 0f;
        public virtual float preferredWidth => EnsureLayout()?.preferredWidth ?? 0f;
        public virtual float flexibleWidth => -1f;
        public virtual float minHeight => 0f;
        public virtual float preferredHeight => EnsureLayout()?.preferredHeight ?? 0f;
        public virtual float flexibleHeight => -1f;
        public virtual int layoutPriority => 0;
    }
}
