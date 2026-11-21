using System;
using System.Collections.Generic;

namespace CosmicShore.Engine.UI
{
    /// <summary>
    /// TextMeshPro layout engine. Turns a <see cref="TMP_Text"/>'s serialized settings, its
    /// font asset chain and the RectTransform rect into SDF glyph quads, reproducing
    /// TextMeshProUGUI's layout behaviour: point-size scaling
    /// (<c>fontSize / faceInfo.pointSize × faceInfo.scale</c>), ascender-anchored lines,
    /// the six vertical and six horizontal alignments, margins, word wrap, character /
    /// word / line / paragraph spacing in em/100, kerning, auto-size (TMP's 0.05-step
    /// bisection between fontSizeMin and fontSizeMax, plus character-width compression),
    /// the overflow modes, font styles and the rich-text tags the game uses.
    /// <para>Implemented from TMP's observable behaviour and its documented units; no
    /// TextMeshPro source was used (Unity Companion License).</para>
    /// <para>Why it lives in the Engine and not the content bridge: TMP_Text's layout is
    /// part of the engine surface ported code relies on (preferred sizes, text bounds,
    /// character info), and it needs nothing but engine types — the content bridge only
    /// LOADS fonts; the renderer (GL client or <see cref="TmpSoftwareRaster"/>) only DRAWS
    /// the quads this produces.</para>
    /// </summary>
    public static class TmpLayout
    {
        /// <summary>TMP's small-caps glyph scale.</summary>
        public const float SmallCapsMultiplier = 0.8f;
        /// <summary>U+25A1, the glyph TMP substitutes when a character is missing everywhere.</summary>
        public const uint DefaultMissingGlyph = 0x25A1;
        public const uint Ellipsis = 0x2026;

        sealed class Elem
        {
            public uint unicode;
            public int sourceIndex;
            public TMP_FontAsset font;
            public TMP_Character ch;
            public Glyph glyph;
            public bool isLineFeed, isWhitespace, isBreakAfter, isControl, isVisible, hidden;
            public bool bold, italic, underline, strike, nobr;
            public bool hasMark;
            public Color32 color, markColor;
            // <size>: 0 = base × value, 1 = absolute value (points), 2 = base + value. TMP resolves
            // every size tag against the component's BASE size, never against an outer tag.
            public int sizeMode;
            public float sizeValue = 1f;
            public float cspaceUnits, cspaceEm, monoUnits, monoEm, voffsetUnits, voffsetEm, spaceUnits, spaceEm;
            public float? posUnits;
            public float smallCaps = 1f;
            public int supSub; // 1 sup, 2 sub
            public HorizontalAlignmentOptions? lineAlign;
            // kerning (font units)
            public float kernAdvance, kernPlaceX, kernPlaceY;
            // per-pass
            public float fontSize, fontScale, elementScale, emScale, advance, ascender, descender, baselineShift, x, cspace, mono;
        }

        sealed class Line
        {
            public int start, end; // inclusive range into elems (end < start = empty)
            public bool endsWithLineFeed;
            public int ellipsis = -1;
            public float ascender, descender, offset, maxAdvance;
            public int lastVisible = -1;
        }

        sealed class Pass
        {
            public readonly List<Line> lines = new();
            public bool forcedCharBreak, widthOverflow;
            public float maxAscender, maxCapHeight, lowestDescender, textHeight, widestLine;
            public float size, widthAdj;
        }

        /// <summary>Lays out <paramref name="text"/> inside <paramref name="rect"/> (RectTransform local space).</summary>
        public static TmpLayoutResult Layout(TMP_Text text, Rect rect, ITmpFontResolver resolver = null)
        {
            resolver ??= TmpSettingsFontResolver.Instance;
            var result = new TmpLayoutResult { rect = rect, clipRect = rect };
            var font = text.font ?? resolver.DefaultFontAsset;
            var material = text.fontSharedMaterial ?? font?.material;
            result.material = material;
            if (font == null) { result.fontSize = text.fontSize; return result; }

            var tokens = TmpRichText.Tokenize(text.text, text.richText, text.parseCtrlCharacters);
            var baseColor = (Color32)text.color;
            var elems = BuildElements(tokens, text, font, baseColor, resolver);
            if (text.enableKerning) ApplyKerning(elems);

            var margin = text.margin;
            float widthAvail = rect.width - margin.x - margin.z;
            float heightAvail = rect.height - margin.y - margin.w;
            bool wrap = text.textWrappingMode == TextWrappingModes.Normal || text.textWrappingMode == TextWrappingModes.PreserveWhitespace;
            bool justified = text.horizontalAlignment == HorizontalAlignmentOptions.Justified || text.horizontalAlignment == HorizontalAlignmentOptions.Flush;

            // ── size selection (auto-size) ────────────────────────────────────────
            float size = text.fontSize;
            float widthAdj = 0f;
            Pass pass;
            if (text.enableAutoSizing)
            {
                float minSize = text.fontSizeMin, maxSize = text.fontSizeMax;
                float lo = minSize, hi = maxSize;
                float maxAdj = Math.Max(0f, text.characterWidthAdjustment) / 100f;
                size = maxSize;
                int iterations = 0;
                while (true)
                {
                    iterations++;
                    pass = RunPass(elems, text, font, size, widthAdj, widthAvail, wrap, justified, resolver, trackForced: true);
                    bool heightFail = pass.textHeight > heightAvail + 0.0001f;
                    bool widthFail = pass.forcedCharBreak || (!wrap && pass.widthOverflow);
                    if (iterations > 100) break;
                    if (heightFail || widthFail)
                    {
                        // Compress character width first (TMP's charWidthMaxAdj), then shrink the font.
                        if (widthFail && !heightFail && maxAdj > 0f && widthAdj < maxAdj - 1e-5f && pass.widestLine > 0f)
                        {
                            float need = 1f - (widthAvail - 0.0001f) / (pass.widestLine / (1f - widthAdj));
                            widthAdj = Math.Min(maxAdj, Math.Max(widthAdj + 0.0001f, need));
                            continue;
                        }
                        if (size <= minSize + 0.0001f) break;
                        hi = size;
                        float delta = Math.Max((size - lo) / 2f, 0.05f);
                        size = Math.Max((int)((size - delta) * 20f + 0.5f) / 20f, minSize);
                        continue;
                    }
                    if (size >= maxSize - 0.0001f || hi - lo <= 0.051f) break;
                    lo = size;
                    float up = Math.Max((hi - size) / 2f, 0.05f);
                    float next = Math.Min((int)((size + up) * 20f + 0.5f) / 20f, maxSize);
                    if (next >= hi - 0.0001f) break; // hi is known to fail
                    size = next;
                }
                // make sure the chosen size is the last one that fit
                pass = RunPass(elems, text, font, size, widthAdj, widthAvail, wrap, justified, resolver, trackForced: false);
                result.autoSizeIterations = iterations;
            }
            else
            {
                pass = RunPass(elems, text, font, size, widthAdj, widthAvail, wrap, justified, resolver, trackForced: false);
            }
            result.fontSize = size;
            result.characterWidthAdjustment = widthAdj;

            // ── overflow handling ────────────────────────────────────────────────
            var mode = text.overflowMode;
            bool heightOverflow = pass.textHeight > heightAvail + 0.0001f;
            result.overflowed = heightOverflow || (!wrap && pass.widthOverflow);
            result.isMasked = mode == TextOverflowModes.Masking;
            if (mode == TextOverflowModes.Ellipsis || mode == TextOverflowModes.Truncate || mode == TextOverflowModes.Page || mode == TextOverflowModes.Linked)
                ApplyTruncation(elems, pass, text, font, resolver, widthAvail, heightAvail, wrap, ellipsis: mode == TextOverflowModes.Ellipsis);

            // maxVisibleCharacters
            int visibleSeen = 0;
            foreach (var e in elems)
            {
                if (e.isLineFeed) continue;
                if (visibleSeen >= text.maxVisibleCharacters) e.hidden = true;
                visibleSeen++;
            }

            RecomputeLineMetrics(elems, pass, text, font, resolver);

            // ── alignment ────────────────────────────────────────────────────────
            float padding = GetPadding(material, text.extraPadding, out float gradientScale, out float ratioA);
            float boldStylePad = font.boldStyle / 4f * gradientScale * ratioA;
            float normalStylePad = font.normalStyle / 4f * gradientScale * ratioA;

            float top = rect.y + rect.height, bottom = rect.y;
            float centerY = (top + bottom) * 0.5f;
            float baseline0;
            switch (text.verticalAlignment)
            {
                case VerticalAlignmentOptions.Middle:
                    baseline0 = centerY - (pass.maxAscender + margin.y + pass.lowestDescender - margin.w) * 0.5f;
                    break;
                case VerticalAlignmentOptions.Bottom:
                    baseline0 = bottom + margin.w - pass.lowestDescender;
                    break;
                case VerticalAlignmentOptions.Baseline:
                    baseline0 = centerY;
                    break;
                case VerticalAlignmentOptions.Geometry:
                {
                    InkExtents(elems, pass, text.characterHorizontalScale, widthAdj, out _, out float minY, out _, out float maxY);
                    baseline0 = centerY - (maxY + margin.y + minY - margin.w) * 0.5f;
                    break;
                }
                case VerticalAlignmentOptions.Capline:
                    baseline0 = centerY - (pass.maxCapHeight - margin.y - margin.w) * 0.5f;
                    break;
                default: // Top
                    baseline0 = top - margin.y - pass.maxAscender;
                    break;
            }

            float left = rect.x + margin.x;
            var inkMin = new Vector2(float.MaxValue, float.MaxValue);
            var inkMax = new Vector2(float.MinValue, float.MinValue);
            var highlightQuads = new List<TmpQuad>();
            var decorationQuads = new List<TmpQuad>();
            var glyphQuads = new List<TmpQuad>();
            float shear = font.italicStyle * 0.01f;
            float hs = text.characterHorizontalScale <= 0f ? 1f : text.characterHorizontalScale;

            for (int li = 0; li < pass.lines.Count; li++)
            {
                var line = pass.lines[li];
                var lineAlign = text.horizontalAlignment;
                for (int k = line.start; k <= line.end; k++)
                    if (elems[k].lineAlign.HasValue) { lineAlign = elems[k].lineAlign.Value; break; }

                float alignOffset = 0f;
                float justifySpace = 0f, justifyChar = 0f;
                switch (lineAlign)
                {
                    case HorizontalAlignmentOptions.Center: alignOffset = widthAvail * 0.5f - line.maxAdvance * 0.5f; break;
                    case HorizontalAlignmentOptions.Right: alignOffset = widthAvail - line.maxAdvance; break;
                    case HorizontalAlignmentOptions.Geometry:
                    {
                        LineInk(elems, line, hs, widthAdj, out float lx0, out float lx1);
                        alignOffset = widthAvail * 0.5f - (lx0 + lx1) * 0.5f;
                        break;
                    }
                    case HorizontalAlignmentOptions.Justified:
                    case HorizontalAlignmentOptions.Flush:
                    {
                        bool lastLine = li == pass.lines.Count - 1 || line.endsWithLineFeed;
                        if (lineAlign == HorizontalAlignmentOptions.Flush || !lastLine)
                        {
                            float gap = widthAvail - line.maxAdvance;
                            int spaces = 0, visible = 0;
                            for (int k = line.start; k <= line.lastVisible && k <= line.end; k++)
                            {
                                if (elems[k].hidden) continue;
                                if (elems[k].isWhitespace) spaces++; else if (elems[k].isVisible) visible++;
                            }
                            if (gap > 0f)
                            {
                                float ratio = spaces > 0 ? text.wordWrappingRatios : 1f;
                                if (spaces > 0) justifySpace = gap * (1f - ratio) / spaces;
                                if (visible > 1) justifyChar = gap * ratio / (visible - 1);
                            }
                        }
                        break;
                    }
                }

                float baseline = baseline0 - line.offset;
                var info = new TmpLineInfo
                {
                    firstCharacterIndex = line.start,
                    lastCharacterIndex = line.end,
                    lastVisibleCharacterIndex = line.lastVisible,
                    maxAdvance = line.maxAdvance,
                    width = widthAvail,
                    ascender = baseline + line.ascender,
                    descender = baseline + line.descender,
                    baseline = baseline,
                    lineOffset = line.offset,
                    endsWithLineFeed = line.endsWithLineFeed,
                    alignment = lineAlign,
                    alignmentOffset = alignOffset,
                };

                float justifyAccum = 0f;
                int count = 0, visibleCount = 0, spaceCount = 0;
                foreach (int k in LineIndices(line))
                {
                    var e = elems[k];
                    count++;
                    if (e.isWhitespace) spaceCount++;
                    float penX = left + alignOffset + e.x + justifyAccum;
                    if (!e.hidden)
                    {
                        if (e.isWhitespace && k <= line.lastVisible) justifyAccum += justifySpace;
                    }
                    var ci = new TmpCharacterInfo
                    {
                        character = e.unicode, sourceIndex = e.sourceIndex, lineNumber = li, font = e.font, glyph = e.glyph,
                        pointSize = e.fontSize, scale = e.elementScale, origin = e.x, xAdvance = e.x + e.advance,
                        baseLine = baseline + e.baselineShift, ascender = baseline + e.ascender, descender = baseline + e.descender,
                        color = e.color, isVisible = e.isVisible && !e.hidden,
                        style = (e.bold ? FontStyles.Bold : 0) | (e.italic ? FontStyles.Italic : 0) | (e.underline ? FontStyles.Underline : 0) | (e.strike ? FontStyles.Strikethrough : 0),
                    };
                    if (e.isVisible && !e.hidden)
                    {
                        visibleCount++;
                        var q = BuildGlyphQuad(e, penX, baseline, padding, e.bold ? boldStylePad : normalStylePad, shear, hs, widthAdj, text, material);
                        q.characterIndex = result.characters.Count;
                        glyphQuads.Add(q);
                        var g = e.glyph;
                        float s = e.elementScale;
                        float gx0 = penX + MonoShift(e, hs, widthAdj) + (g.metrics.horizontalBearingX * hs + e.kernPlaceX) * s * (1f - widthAdj);
                        float gx1 = gx0 + g.metrics.width * hs * s * (1f - widthAdj);
                        float gy1 = baseline + e.baselineShift + (g.metrics.horizontalBearingY + e.kernPlaceY) * s;
                        float gy0 = gy1 - g.metrics.height * s;
                        ci.bottomLeft = new Vector2(gx0, gy0);
                        ci.topRight = new Vector2(gx1, gy1);
                        inkMin = new Vector2(Math.Min(inkMin.x, gx0), Math.Min(inkMin.y, gy0));
                        inkMax = new Vector2(Math.Max(inkMax.x, gx1), Math.Max(inkMax.y, gy1));
                        if (justifyChar != 0f) justifyAccum += justifyChar;
                    }
                    else
                    {
                        ci.bottomLeft = new Vector2(penX, baseline + e.descender);
                        ci.topRight = new Vector2(penX + e.advance, baseline + e.ascender);
                    }
                    result.characters.Add(ci);
                }
                info.characterCount = count;
                info.visibleCharacterCount = visibleCount;
                info.spaceCount = spaceCount;
                result.lines.Add(info);

                BuildDecorations(elems, line, left + alignOffset, baseline, padding, text, material, highlightQuads, decorationQuads);
            }

            result.quads.AddRange(highlightQuads);
            result.quads.AddRange(glyphQuads);
            result.quads.AddRange(decorationQuads);
            result.textBounds = inkMin.x <= inkMax.x ? Rect.MinMaxRect(inkMin.x, inkMin.y, inkMax.x, inkMax.y) : new Rect(rect.x + rect.width * 0.5f, rect.y + rect.height * 0.5f, 0, 0);
            result.preferredHeight = pass.textHeight + margin.y + margin.w;
            var unwrapped = RunPass(elems, text, font, size, widthAdj, float.MaxValue, false, false, resolver, false);
            result.preferredWidth = unwrapped.widestLine + margin.x + margin.z;
            return result;
        }

        static IEnumerable<int> LineIndices(Line line)
        {
            for (int k = line.start; k <= line.end; k++) yield return k;
            if (line.ellipsis >= 0) yield return line.ellipsis;
        }

        // ── element building ──────────────────────────────────────────────────────

        static List<Elem> BuildElements(List<TmpToken> tokens, TMP_Text text, TMP_FontAsset font, Color32 baseColor, ITmpFontResolver resolver)
        {
            var elems = new List<Elem>(tokens.Count);
            var colorStack = new Stack<Color32>();
            var sizeStack = new Stack<(int mode, float value)>();
            var markStack = new Stack<Color32>();
            var alignStack = new Stack<HorizontalAlignmentOptions>();
            int bold = 0, italic = 0, underline = 0, strike = 0, upper = 0, lower = 0, smallCaps = 0, sup = 0, sub = 0, nobr = 0;
            var style = text.fontStyle;
            Color32 color = baseColor;
            byte? alphaOverride = null;
            int sizeMode = 0; float sizeValue = 1f;
            float cspU = 0, cspE = 0, monoU = 0, monoE = 0, voffU = 0, voffE = 0;
            float pendingSpaceU = 0, pendingSpaceE = 0;
            float? pendingPos = null;
            HorizontalAlignmentOptions? align = null;

            foreach (var t in tokens)
            {
                if (t.IsTag)
                {
                    string v = t.TagValue;
                    switch (t.TagName)
                    {
                        case "b": bold += t.IsClosing ? -1 : 1; break;
                        case "i": italic += t.IsClosing ? -1 : 1; break;
                        case "u": underline += t.IsClosing ? -1 : 1; break;
                        case "s": case "strikethrough": strike += t.IsClosing ? -1 : 1; break;
                        case "uppercase": case "allcaps": upper += t.IsClosing ? -1 : 1; break;
                        case "lowercase": lower += t.IsClosing ? -1 : 1; break;
                        case "smallcaps": smallCaps += t.IsClosing ? -1 : 1; break;
                        case "sup": sup += t.IsClosing ? -1 : 1; break;
                        case "sub": sub += t.IsClosing ? -1 : 1; break;
                        case "nobr": nobr += t.IsClosing ? -1 : 1; break;
                        case "color":
                            if (t.IsClosing) color = colorStack.Count > 0 ? colorStack.Pop() : baseColor;
                            else if (TmpRichText.TryParseColor(v, out var c)) { colorStack.Push(color); color = c; }
                            break;
                        case "alpha":
                            if (t.IsClosing) alphaOverride = null;
                            else if (TmpRichText.TryParseColor("#FFFFFF" + (v?.TrimStart('#') ?? "FF"), out var ac)) alphaOverride = ac.a;
                            break;
                        case "mark":
                            if (t.IsClosing) { if (markStack.Count > 0) markStack.Pop(); }
                            else markStack.Push(TmpRichText.TryParseColor(v, out var mc) ? mc : new Color32(255, 255, 0, 64));
                            break;
                        case "size":
                            if (t.IsClosing) { (sizeMode, sizeValue) = sizeStack.Count > 0 ? sizeStack.Pop() : (0, 1f); }
                            else if (TmpRichText.TryParseLength(v, out float n, out var unit, out bool rel))
                            {
                                sizeStack.Push((sizeMode, sizeValue));
                                switch (unit)
                                {
                                    case TmpRichText.Unit.Percent: sizeMode = 0; sizeValue = n / 100f; break;
                                    case TmpRichText.Unit.Em: sizeMode = 0; sizeValue = n; break;
                                    default:
                                        if (rel) { sizeMode = 2; sizeValue = n; }
                                        else { sizeMode = 1; sizeValue = n; }
                                        break;
                                }
                            }
                            break;
                        case "cspace":
                            cspU = cspE = 0;
                            if (!t.IsClosing && TmpRichText.TryParseLength(v, out float cs, out var cu, out _)) { if (cu == TmpRichText.Unit.Em) cspE = cs; else cspU = cs; }
                            break;
                        case "mspace":
                            monoU = monoE = 0;
                            if (!t.IsClosing && TmpRichText.TryParseLength(v, out float ms, out var mu, out _)) { if (mu == TmpRichText.Unit.Em) monoE = ms; else monoU = ms; }
                            break;
                        case "voffset":
                            voffU = voffE = 0;
                            if (!t.IsClosing && TmpRichText.TryParseLength(v, out float vo, out var vu, out _)) { if (vu == TmpRichText.Unit.Em) voffE = vo; else voffU = vo; }
                            break;
                        case "space":
                            if (!t.IsClosing && TmpRichText.TryParseLength(v, out float sp, out var su, out _)) { if (su == TmpRichText.Unit.Em) pendingSpaceE += sp; else pendingSpaceU += sp; }
                            break;
                        case "pos":
                            if (!t.IsClosing && TmpRichText.TryParseLength(v, out float ps, out var pu, out _) && pu == TmpRichText.Unit.Pixels) pendingPos = ps;
                            break;
                        case "align":
                            if (t.IsClosing) align = alignStack.Count > 0 ? alignStack.Pop() : null;
                            else
                            {
                                if (align.HasValue) alignStack.Push(align.Value);
                                align = (v ?? "").ToLowerInvariant() switch
                                {
                                    "left" => HorizontalAlignmentOptions.Left,
                                    "center" => HorizontalAlignmentOptions.Center,
                                    "right" => HorizontalAlignmentOptions.Right,
                                    "justified" => HorizontalAlignmentOptions.Justified,
                                    "flush" => HorizontalAlignmentOptions.Flush,
                                    _ => align,
                                };
                            }
                            break;
                    }
                    continue;
                }

                uint cp = t.Unicode;
                if (cp == 13) continue; // carriage return renders nothing
                var e = new Elem { sourceIndex = t.SourceIndex };
                bool isLower = cp < 0x10000 && char.IsLower((char)cp);
                bool doUpper = upper > 0 || (style & FontStyles.UpperCase) != 0;
                bool doLower = lower > 0 || (style & FontStyles.LowerCase) != 0;
                bool doSmall = smallCaps > 0 || (style & FontStyles.SmallCaps) != 0;
                if (doUpper && cp < 0x10000) cp = char.ToUpperInvariant((char)cp);
                else if (doLower && cp < 0x10000) cp = char.ToLowerInvariant((char)cp);
                else if (doSmall && isLower) { cp = char.ToUpperInvariant((char)cp); e.smallCaps = SmallCapsMultiplier; }
                e.unicode = cp;
                e.bold = bold > 0 || (style & FontStyles.Bold) != 0;
                e.italic = italic > 0 || (style & FontStyles.Italic) != 0;
                e.underline = underline > 0 || (style & FontStyles.Underline) != 0;
                e.strike = strike > 0 || (style & FontStyles.Strikethrough) != 0;
                e.nobr = nobr > 0;
                e.supSub = sup > 0 || (style & FontStyles.Superscript) != 0 ? 1 : (sub > 0 || (style & FontStyles.Subscript) != 0 ? 2 : 0);
                var c32 = color;
                if (alphaOverride.HasValue) c32.a = alphaOverride.Value;
                // TMP: the vertex alpha never exceeds the component's own font colour alpha.
                if (c32.a > baseColor.a) c32.a = baseColor.a;
                e.color = c32;
                if (markStack.Count > 0) { e.hasMark = true; e.markColor = markStack.Peek(); }
                else if ((style & FontStyles.Highlight) != 0) { e.hasMark = true; e.markColor = new Color32(255, 255, 0, 64); }
                e.sizeMode = sizeMode;
                e.sizeValue = sizeValue;
                e.cspaceUnits = cspU; e.cspaceEm = cspE;
                e.monoUnits = monoU; e.monoEm = monoE;
                e.voffsetUnits = voffU; e.voffsetEm = voffE;
                e.spaceUnits = pendingSpaceU; e.spaceEm = pendingSpaceE; pendingSpaceU = pendingSpaceE = 0;
                e.posUnits = pendingPos; pendingPos = null;
                e.lineAlign = align;

                e.isLineFeed = cp == 10 || cp == 11 || cp == 0x2028 || cp == 0x2029;
                e.isWhitespace = !e.isLineFeed && (cp == 32 || cp == 9 || cp == 0x200B || cp == 0xA0 || cp == 0x3000 || (cp >= 0x2000 && cp <= 0x200A));
                e.isBreakAfter = !e.nobr && ((e.isWhitespace && cp != 0xA0) || cp == '-' || cp == 0xAD);
                e.isControl = e.isLineFeed || cp == 0x200B || cp == 0xAD || cp < 9;

                if (!e.isLineFeed && cp != 0x200B && cp != 0xAD && cp != 9)
                {
                    if (!TryResolveCharacter(cp, font, resolver, out e.font, out e.ch))
                    {
                        uint missing = resolver.MissingGlyphCharacter != 0 ? (uint)resolver.MissingGlyphCharacter : DefaultMissingGlyph;
                        TryResolveCharacter(missing, font, resolver, out e.font, out e.ch);
                    }
                }
                if (e.ch == null)
                {
                    // Line feeds, tabs and unresolvable characters take the primary font's metrics.
                    e.font = font;
                    if (cp == 9 || cp == 0x200B || cp == 0xAD) e.font = font;
                }
                e.glyph = e.ch?.glyph;
                e.isVisible = !e.isWhitespace && !e.isControl && e.glyph != null && e.glyph.metrics.width > 0f && e.glyph.metrics.height > 0f;
                elems.Add(e);
            }
            return elems;
        }

        /// <summary>
        /// TMP's character lookup order: the font, its fallback table (depth-first), the
        /// global fallback list, then the default font asset and its fallbacks.
        /// </summary>
        public static bool TryResolveCharacter(uint unicode, TMP_FontAsset font, ITmpFontResolver resolver, out TMP_FontAsset owner, out TMP_Character character)
        {
            var visited = new HashSet<TMP_FontAsset>();
            if (Search(font, unicode, visited, out owner, out character)) return true;
            if (resolver != null)
            {
                foreach (var f in resolver.GlobalFallbackFontAssets)
                    if (Search(f, unicode, visited, out owner, out character)) return true;
                if (Search(resolver.DefaultFontAsset, unicode, visited, out owner, out character)) return true;
            }
            owner = null; character = null;
            return false;
        }

        static bool Search(TMP_FontAsset font, uint unicode, HashSet<TMP_FontAsset> visited, out TMP_FontAsset owner, out TMP_Character character)
        {
            owner = null; character = null;
            if (font == null || !visited.Add(font)) return false;
            if (font.TryGetCharacter(unicode, out character) && character.glyph != null) { owner = font; return true; }
            foreach (var fb in font.fallbackFontAssetTable)
                if (Search(fb, unicode, visited, out owner, out character)) return true;
            return false;
        }

        static void ApplyKerning(List<Elem> elems)
        {
            for (int i = 0; i + 1 < elems.Count; i++)
            {
                var a = elems[i]; var b = elems[i + 1];
                if (a.glyph == null || b.glyph == null || !ReferenceEquals(a.font, b.font)) continue;
                if (a.font.TryGetPairAdjustment(a.glyph.index, b.glyph.index, out var rec))
                {
                    a.kernAdvance += rec.firstAdjustment.xAdvance;
                    a.kernPlaceX += rec.firstAdjustment.xPlacement;
                    a.kernPlaceY += rec.firstAdjustment.yPlacement;
                    b.kernAdvance += rec.secondAdjustment.xAdvance;
                    b.kernPlaceX += rec.secondAdjustment.xPlacement;
                    b.kernPlaceY += rec.secondAdjustment.yPlacement;
                }
            }
        }

        // ── one layout pass at a given size ───────────────────────────────────────

        static void ComputeMetrics(Elem e, TMP_Text text, TMP_FontAsset primary, float baseSize, float widthAdj)
        {
            var font = e.font ?? primary;
            var face = font.faceInfo;
            float ortho = text.isOrthographic ? 1f : 0.1f;
            float size = e.sizeMode switch
            {
                1 => e.sizeValue,
                2 => baseSize + e.sizeValue,
                _ => baseSize * e.sizeValue,
            };
            e.fontSize = size;
            float pointSize = face.pointSize > 0f ? face.pointSize : 1f;
            float faceScale = face.scale > 0f ? face.scale : 1f;
            float fontScale = size / pointSize * faceScale * ortho;
            float baselineShift = 0f;
            if (e.supSub == 1) { baselineShift = face.superscriptOffset * fontScale; fontScale *= face.superscriptSize > 0 ? face.superscriptSize : 0.5f; }
            else if (e.supSub == 2) { baselineShift = face.subscriptOffset * fontScale; fontScale *= face.subscriptSize > 0 ? face.subscriptSize : 0.5f; }
            e.fontScale = fontScale;
            float charScale = (e.ch?.scale ?? 1f) * (e.glyph?.scale ?? 1f);
            e.elementScale = fontScale * charScale * e.smallCaps;
            e.emScale = size * 0.01f * ortho;
            e.cspace = e.cspaceUnits * ortho + e.cspaceEm * size * ortho;
            e.mono = e.monoUnits * ortho + e.monoEm * size * ortho;
            baselineShift += e.voffsetUnits * ortho + e.voffsetEm * size * ortho;
            e.baselineShift = baselineShift;
            e.ascender = face.ascentLine * fontScale * charScale + baselineShift;
            e.descender = face.descentLine * fontScale * charScale + baselineShift;

            float hs = text.characterHorizontalScale <= 0f ? 1f : text.characterHorizontalScale;
            float spacing = text.characterSpacing + (e.bold ? font.boldSpacing : font.normalSpacingOffset);
            float adv;
            if (e.isLineFeed) adv = 0f;
            else if (e.unicode == 0x200B || e.unicode == 0xAD) adv = 0f;
            else if (e.mono != 0f) adv = e.mono + spacing * e.emScale + e.cspace;
            else
            {
                float glyphAdv = e.glyph != null ? e.glyph.metrics.horizontalAdvance * hs : 0f;
                adv = (glyphAdv + e.kernAdvance) * e.elementScale + spacing * e.emScale + e.cspace;
            }
            if (e.isWhitespace && e.unicode != 0x200B) adv += text.wordSpacing * e.emScale;
            e.advance = adv * (1f - widthAdj);
        }

        static Pass RunPass(List<Elem> elems, TMP_Text text, TMP_FontAsset font, float size, float widthAdj,
            float widthAvail, bool wrap, bool justified, ITmpFontResolver resolver, bool trackForced)
        {
            foreach (var e in elems) ComputeMetrics(e, text, font, size, widthAdj);
            var pass = new Pass { size = size, widthAdj = widthAdj };
            float wrapWidth = widthAvail * (justified ? 1.05f : 1f);
            int n = elems.Count;
            int lineStart = 0, lastBreak = -1;
            float pen = 0f;
            int i = 0;
            int guard = 0;
            while (i < n)
            {
                if (++guard > n * 4 + 16) break;
                var e = elems[i];
                if (e.isLineFeed)
                {
                    e.x = pen;
                    pass.lines.Add(new Line { start = lineStart, end = i, endsWithLineFeed = true });
                    lineStart = i + 1; pen = 0f; lastBreak = -1; i++;
                    continue;
                }
                float penHere = pen + e.spaceUnits * (text.isOrthographic ? 1f : 0.1f) + e.spaceEm * e.fontSize * 0.01f;
                if (e.posUnits.HasValue) penHere = e.posUnits.Value;
                if (e.unicode == 9)
                {
                    float tabAdv = font.faceInfo.tabWidth * font.tabSize * e.elementScale;
                    if (tabAdv > 0f)
                    {
                        float tabs = MathF.Ceiling(penHere / tabAdv) * tabAdv;
                        e.advance = tabs > penHere ? tabs - penHere : tabAdv;
                    }
                }
                if (wrap && !e.isWhitespace && !e.isControl && i > lineStart)
                {
                    float glyphAdv = e.glyph != null ? e.glyph.metrics.horizontalAdvance * (text.characterHorizontalScale <= 0 ? 1f : text.characterHorizontalScale) : 0f;
                    float testWidth = penHere + (e.mono != 0 ? e.mono : glyphAdv * e.elementScale) * (1f - widthAdj);
                    if (testWidth > wrapWidth + 0.0001f)
                    {
                        if (lastBreak >= lineStart)
                        {
                            pass.lines.Add(new Line { start = lineStart, end = lastBreak });
                            lineStart = lastBreak + 1; i = lineStart; pen = 0f; lastBreak = -1;
                            continue;
                        }
                        if (trackForced) pass.forcedCharBreak = true;
                        pass.lines.Add(new Line { start = lineStart, end = i - 1 });
                        lineStart = i; pen = 0f; lastBreak = -1;
                        continue;
                    }
                }
                e.x = penHere;
                pen = penHere + e.advance;
                if (e.isBreakAfter) lastBreak = i;
                i++;
            }
            if (lineStart < n || pass.lines.Count == 0 || elems[n - 1].isLineFeed)
            {
                // Trailing text; or empty text (one empty line). A trailing line feed closes its
                // line and TMP adds no metrics for the empty line after it.
                if (lineStart < n || pass.lines.Count == 0)
                    pass.lines.Add(new Line { start = lineStart, end = n - 1 });
            }
            ComputeLineMetrics(elems, pass, text, font, size, widthAvail);
            return pass;
        }

        static void ComputeLineMetrics(List<Elem> elems, Pass pass, TMP_Text text, TMP_FontAsset font, float baseSize, float widthAvail)
        {
            float ortho = text.isOrthographic ? 1f : 0.1f;
            var face = font.faceInfo;
            float basePoint = face.pointSize > 0 ? face.pointSize : 1f;
            float baseFontScale = baseSize / basePoint * (face.scale > 0 ? face.scale : 1f) * ortho;
            float offset = 0f;
            float prevDescender = 0f;
            pass.maxAscender = float.MinValue;
            pass.maxCapHeight = 0f;
            pass.lowestDescender = float.MaxValue;
            pass.widestLine = 0f;
            pass.widthOverflow = false;
            for (int li = 0; li < pass.lines.Count; li++)
            {
                var line = pass.lines[li];
                float asc = float.MinValue, desc = float.MaxValue;
                float startAsc = float.MinValue;
                int lastVisible = -1;
                foreach (int k in LineIndices(line))
                {
                    var e = elems[k];
                    if (e.hidden && k != line.ellipsis) continue;
                    if (startAsc == float.MinValue) startAsc = e.ascender;
                    asc = Math.Max(asc, e.ascender);
                    desc = Math.Min(desc, e.descender);
                    if (e.isVisible && !e.hidden) lastVisible = k;
                    if (li == 0)
                    {
                        var f = e.font ?? font;
                        float cap = f.faceInfo.capLine * e.elementScale / e.smallCaps;
                        pass.maxCapHeight = Math.Max(pass.maxCapHeight, cap);
                    }
                }
                if (asc == float.MinValue)
                {
                    // Empty line: the base font's metrics.
                    asc = face.ascentLine * baseFontScale;
                    desc = face.descentLine * baseFontScale;
                    if (li == 0) pass.maxCapHeight = face.capLine * baseFontScale;
                }
                line.ascender = asc;
                line.descender = desc;
                line.lastVisible = lastVisible;
                if (li > 0)
                {
                    var prev = pass.lines[li - 1];
                    var lf = prev.endsWithLineFeed ? elems[prev.end] : null;
                    var fs = elems.Count > 0 ? elems[Math.Min(line.start, elems.Count - 1)] : null;
                    var metricsElem = lf ?? fs;
                    var mf = metricsElem?.font ?? font;
                    float scale = metricsElem?.fontScale ?? baseFontScale;
                    float em = metricsElem?.emScale ?? baseSize * 0.01f * ortho;
                    float lineGap = mf.faceInfo.lineHeight - (mf.faceInfo.ascentLine - mf.faceInfo.descentLine);
                    float paragraph = prev.endsWithLineFeed ? text.paragraphSpacing : 0f;
                    offset += -prevDescender + asc + lineGap * scale + (text.lineSpacing + paragraph) * em;
                }
                line.offset = offset;
                prevDescender = desc;
                if (li == 0) pass.maxAscender = asc;
                pass.lowestDescender = Math.Min(pass.lowestDescender, desc - offset);

                if (lastVisible >= 0)
                {
                    var lv = elems[lastVisible];
                    var lvf = lv.font ?? font;
                    line.maxAdvance = lv.x + lv.advance - ((text.characterSpacing + lvf.normalSpacingOffset) * lv.emScale + lv.cspace) * (1f - 0f);
                }
                else line.maxAdvance = 0f;
                pass.widestLine = Math.Max(pass.widestLine, line.maxAdvance);
                if (line.maxAdvance > widthAvail + 0.0001f) pass.widthOverflow = true;
            }
            if (pass.lines.Count == 0) { pass.maxAscender = face.ascentLine * baseFontScale; pass.lowestDescender = face.descentLine * baseFontScale; }
            pass.textHeight = pass.maxAscender - pass.lowestDescender;
        }

        static void RecomputeLineMetrics(List<Elem> elems, Pass pass, TMP_Text text, TMP_FontAsset font, ITmpFontResolver resolver)
        {
            // Re-derive ascenders/offsets/maxAdvance after truncation (hidden characters excluded).
            ComputeLineMetrics(elems, pass, text, font, pass.size, float.MaxValue);
        }

        // ── overflow: truncate / ellipsis ─────────────────────────────────────────

        static void ApplyTruncation(List<Elem> elems, Pass pass, TMP_Text text, TMP_FontAsset font, ITmpFontResolver resolver,
            float widthAvail, float heightAvail, bool wrap, bool ellipsis)
        {
            // Height: keep lines whose bottom (descender) fits; the last kept line gets the ellipsis.
            int lastKept = pass.lines.Count - 1;
            if (pass.textHeight > heightAvail + 0.0001f)
            {
                lastKept = -1;
                for (int li = 0; li < pass.lines.Count; li++)
                {
                    var line = pass.lines[li];
                    float h = pass.maxAscender - (line.descender - line.offset);
                    if (h <= heightAvail + 0.0001f) lastKept = li; else break;
                }
                for (int li = Math.Max(0, lastKept + 1); li < pass.lines.Count; li++)
                    foreach (int k in LineIndices(pass.lines[li])) elems[k].hidden = true;
                if (lastKept < 0) { pass.lines.RemoveRange(1, pass.lines.Count - 1); return; }
                pass.lines.RemoveRange(lastKept + 1, pass.lines.Count - lastKept - 1);
                if (ellipsis && lastKept < pass.lines.Count) AddEllipsis(elems, pass.lines[lastKept], text, font, resolver, widthAvail, pass.size, pass.widthAdj);
                if (wrap) return;
            }

            // Width (no wrap): cut each line at the first character that crosses the right edge.
            if (!wrap)
            {
                foreach (var line in pass.lines)
                {
                    int cut = -1;
                    for (int k = line.start; k <= line.end; k++)
                    {
                        var e = elems[k];
                        if (e.hidden || !e.isVisible) continue;
                        if (e.x + e.advance > widthAvail + 0.0001f) { cut = k; break; }
                    }
                    if (cut < 0) continue;
                    for (int k = cut; k <= line.end; k++) elems[k].hidden = true;
                    if (ellipsis) AddEllipsis(elems, line, text, font, resolver, widthAvail, pass.size, pass.widthAdj);
                }
            }
        }

        static void AddEllipsis(List<Elem> elems, Line line, TMP_Text text, TMP_FontAsset font, ITmpFontResolver resolver, float widthAvail, float baseSize, float widthAdj)
        {
            if (line.ellipsis >= 0) return;
            // Style reference: last non-hidden character of the line (or its first).
            Elem reference = null;
            for (int k = line.end; k >= line.start; k--) if (!elems[k].hidden && !elems[k].isLineFeed) { reference = elems[k]; break; }
            reference ??= line.start < elems.Count ? elems[Math.Max(0, line.start)] : null;
            if (reference == null) return;
            if (!TryResolveCharacter(Ellipsis, reference.font ?? font, resolver, out var ef, out var ech)) return;
            var el = new Elem
            {
                unicode = Ellipsis, sourceIndex = reference.sourceIndex, font = ef, ch = ech, glyph = ech.glyph,
                bold = reference.bold, italic = reference.italic, color = reference.color,
                sizeMode = reference.sizeMode, sizeValue = reference.sizeValue, smallCaps = 1f,
                isVisible = true,
            };
            ComputeMetrics(el, text, font, baseSize, widthAdj);
            // Remove characters from the end until pen + ellipsis fits.
            while (true)
            {
                int last = -1;
                for (int k = line.end; k >= line.start; k--)
                    if (!elems[k].hidden && !elems[k].isLineFeed) { last = k; break; }
                float pen = last >= 0 ? elems[last].x + elems[last].advance : 0f;
                bool trailingWhitespace = last >= 0 && elems[last].isWhitespace;
                if (last >= 0 && (trailingWhitespace || pen + el.advance > widthAvail + 0.0001f))
                {
                    elems[last].hidden = true;
                    continue;
                }
                el.x = pen;
                break;
            }
            elems.Add(el);
            line.ellipsis = elems.Count - 1;
            if (line.endsWithLineFeed) elems[line.end].hidden = true;
        }

        // ── quads ─────────────────────────────────────────────────────────────────

        static float MonoShift(Elem e, float hs, float widthAdj)
        {
            if (e.mono == 0f || e.glyph == null) return 0f;
            return e.mono * 0.5f - (e.glyph.metrics.width * hs * 0.5f + e.glyph.metrics.horizontalBearingX * hs) * e.elementScale * (1f - widthAdj);
        }

        static TmpQuad BuildGlyphQuad(Elem e, float penX, float baseline, float padding, float stylePad, float shear, float hs,
            float widthAdj, TMP_Text text, Material material)
        {
            var g = e.glyph;
            float s = e.elementScale;
            float wadj = 1f - widthAdj;
            float x0 = penX + MonoShift(e, hs, widthAdj) + (g.metrics.horizontalBearingX * hs - padding - stylePad + e.kernPlaceX) * s * wadj;
            float x1 = x0 + (g.metrics.width * hs + padding * 2f + stylePad * 2f) * s * wadj;
            float yBase = baseline + e.baselineShift;
            float y1 = yBase + (g.metrics.horizontalBearingY + padding + e.kernPlaceY) * s;
            float y0 = y1 - (g.metrics.height + padding * 2f) * s;

            float topShear = 0f, bottomShear = 0f;
            if (e.italic)
            {
                topShear = shear * (g.metrics.horizontalBearingY + padding + stylePad) * s;
                bottomShear = shear * (g.metrics.horizontalBearingY - g.metrics.height - padding - stylePad) * s;
            }

            var font = e.font;
            float W = Math.Max(1, font.atlasWidth), H = Math.Max(1, font.atlasHeight);
            var r = g.glyphRect;
            float u0 = (r.x - padding - stylePad) / W, u1 = (r.x + r.width + padding + stylePad) / W;
            float v0 = (r.y - padding) / H, v1 = (r.y + r.height + padding) / H;

            GradientColors(text, e.color, out var cbl, out var ctl, out var ctr, out var cbr);
            float vs = e.bold ? -s * wadj : s * wadj;
            return new TmpQuad
            {
                kind = TmpQuadKind.Glyph,
                font = font,
                material = ReferenceEquals(font, text.font) || text.font == null ? material : font.material ?? material,
                atlasIndex = g.atlasIndex,
                bl = new TmpVertex(new Vector3(x0 + bottomShear, y0, 0), new Vector2(u0, v0), cbl, vs),
                tl = new TmpVertex(new Vector3(x0 + topShear, y1, 0), new Vector2(u0, v1), ctl, vs),
                tr = new TmpVertex(new Vector3(x1 + topShear, y1, 0), new Vector2(u1, v1), ctr, vs),
                br = new TmpVertex(new Vector3(x1 + bottomShear, y0, 0), new Vector2(u1, v0), cbr, vs),
            };
        }

        static void GradientColors(TMP_Text text, Color32 c, out Color32 bl, out Color32 tl, out Color32 tr, out Color32 br)
        {
            if (!text.enableVertexGradient) { bl = tl = tr = br = c; return; }
            var g = text.colorGradient;
            Color cc = c;
            Color gtl = g.topLeft, gtr = g.topRight, gbl = g.bottomLeft, gbr = g.bottomRight;
            switch (text.colorMode)
            {
                case ColorMode.Single: gtr = gbl = gbr = gtl; break;
                case ColorMode.HorizontalGradient: gbl = gtl; gbr = gtr; break;
                case ColorMode.VerticalGradient: gtr = gtl; gbr = gbl; break;
            }
            tl = Mul(cc, gtl); tr = Mul(cc, gtr); bl = Mul(cc, gbl); br = Mul(cc, gbr);
        }

        static Color32 Mul(Color a, Color b) => new Color(a.r * b.r, a.g * b.g, a.b * b.b, a.a * b.a);

        static void BuildDecorations(List<Elem> elems, Line line, float lineLeft, float baseline, float padding, TMP_Text text,
            Material material, List<TmpQuad> highlights, List<TmpQuad> decorations)
        {
            // Runs of consecutive (non-hidden) characters sharing a decoration, trailing whitespace excluded.
            void Runs(Func<Elem, bool> has, Action<int, int> emit)
            {
                int runStart = -1, runEnd = -1;
                foreach (int k in LineIndices(line))
                {
                    var e = elems[k];
                    bool on = !e.hidden && !e.isLineFeed && has(e);
                    if (on)
                    {
                        if (runStart < 0) runStart = k;
                        if (!e.isWhitespace) runEnd = k;
                    }
                    else if (runStart >= 0)
                    {
                        if (runEnd >= runStart) emit(runStart, runEnd);
                        runStart = runEnd = -1;
                    }
                }
                if (runStart >= 0 && runEnd >= runStart) emit(runStart, runEnd);
            }

            Runs(e => e.hasMark, (a, b) =>
            {
                var ea = elems[a];
                if (!TryDecorationGlyph(ea, out var font, out var g)) return;
                float x0 = lineLeft + ea.x - padding * ea.elementScale;
                float x1 = lineLeft + elems[b].x + elems[b].advance + padding * elems[b].elementScale;
                float asc = float.MinValue, desc = float.MaxValue;
                for (int k = a; k <= b && k < elems.Count; k++) { asc = Math.Max(asc, elems[k].ascender); desc = Math.Min(desc, elems[k].descender); }
                float W = font.atlasWidth, H = font.atlasHeight;
                var uv = new Vector2((g.glyphRect.x + g.glyphRect.width * 0.5f) / W, (g.glyphRect.y + g.glyphRect.height * 0.5f) / H);
                var c = ea.markColor;
                float vs = ea.elementScale;
                highlights.Add(new TmpQuad
                {
                    kind = TmpQuadKind.Highlight, font = font, material = material, atlasIndex = g.atlasIndex,
                    bl = new TmpVertex(new Vector3(x0, baseline + desc, 0), uv, c, vs),
                    tl = new TmpVertex(new Vector3(x0, baseline + asc, 0), uv, c, vs),
                    tr = new TmpVertex(new Vector3(x1, baseline + asc, 0), uv, c, vs),
                    br = new TmpVertex(new Vector3(x1, baseline + desc, 0), uv, c, vs),
                });
            });

            Runs(e => e.underline, (a, b) => AddLine(elems, a, b, lineLeft, baseline, padding, material, decorations, TmpQuadKind.Underline));
            Runs(e => e.strike, (a, b) => AddLine(elems, a, b, lineLeft, baseline, padding, material, decorations, TmpQuadKind.Strikethrough));
        }

        static bool TryDecorationGlyph(Elem e, out TMP_FontAsset font, out Glyph glyph)
        {
            font = e.font; glyph = null;
            if (font == null) return false;
            if (font.TryGetCharacter('_', out var ch) && ch.glyph != null && ch.glyph.glyphRect.width > 0) { glyph = ch.glyph; return true; }
            return false;
        }

        static void AddLine(List<Elem> elems, int a, int b, float lineLeft, float baseline, float padding, Material material,
            List<TmpQuad> output, TmpQuadKind kind)
        {
            var ea = elems[a];
            if (!TryDecorationGlyph(ea, out var font, out var g)) return;
            float s = 0f;
            for (int k = a; k <= b && k < elems.Count; k++) s = Math.Max(s, elems[k].fontScale);
            var face = font.faceInfo;
            float offset = kind == TmpQuadKind.Underline ? face.underlineOffset : face.strikethroughOffset;
            float thickness = kind == TmpQuadKind.Underline ? face.underlineThickness : face.strikethroughThickness;
            if (thickness <= 0f) thickness = g.metrics.height;
            float yTop = baseline + ea.baselineShift + (offset + padding) * s;
            float yBot = yTop - (thickness + padding * 2f) * s;
            float x0 = lineLeft + ea.x - padding * s;
            float x1 = lineLeft + elems[b].x + elems[b].advance + padding * s;
            float W = font.atlasWidth, H = font.atlasHeight;
            var r = g.glyphRect;
            float seg = (r.width * 0.5f + padding) * s;
            if (x1 - x0 < seg * 2f) seg = (x1 - x0) * 0.5f;
            float uL = (r.x - padding) / W, uM = (r.x + r.width * 0.5f) / W, uR = (r.x + r.width + padding) / W;
            float v0 = (r.y - padding) / H, v1 = (r.y + r.height + padding) / H;
            var c = ea.color;
            void Quad(float qx0, float qx1, float qu0, float qu1)
                => output.Add(new TmpQuad
                {
                    kind = kind, font = font, material = material, atlasIndex = g.atlasIndex,
                    bl = new TmpVertex(new Vector3(qx0, yBot, 0), new Vector2(qu0, v0), c, s),
                    tl = new TmpVertex(new Vector3(qx0, yTop, 0), new Vector2(qu0, v1), c, s),
                    tr = new TmpVertex(new Vector3(qx1, yTop, 0), new Vector2(qu1, v1), c, s),
                    br = new TmpVertex(new Vector3(qx1, yBot, 0), new Vector2(qu1, v0), c, s),
                });
            Quad(x0, x0 + seg, uL, uM);
            if (x1 - seg > x0 + seg) Quad(x0 + seg, x1 - seg, uM, uM);
            Quad(x1 - seg, x1, uM, uR);
        }

        static void LineInk(List<Elem> elems, Line line, float hs, float widthAdj, out float x0, out float x1)
        {
            x0 = float.MaxValue; x1 = float.MinValue;
            foreach (int k in LineIndices(line))
            {
                var e = elems[k];
                if (e.hidden || !e.isVisible) continue;
                float gx0 = e.x + MonoShift(e, hs, widthAdj) + (e.glyph.metrics.horizontalBearingX * hs + e.kernPlaceX) * e.elementScale * (1f - widthAdj);
                x0 = Math.Min(x0, gx0);
                x1 = Math.Max(x1, gx0 + e.glyph.metrics.width * hs * e.elementScale * (1f - widthAdj));
            }
            if (x0 > x1) { x0 = x1 = 0f; }
        }

        static void InkExtents(List<Elem> elems, Pass pass, float hs, float widthAdj, out float minX, out float minY, out float maxX, out float maxY)
        {
            minX = minY = float.MaxValue; maxX = maxY = float.MinValue;
            foreach (var line in pass.lines)
                foreach (int k in LineIndices(line))
                {
                    var e = elems[k];
                    if (e.hidden || !e.isVisible) continue;
                    float top = -line.offset + e.baselineShift + (e.glyph.metrics.horizontalBearingY + e.kernPlaceY) * e.elementScale;
                    float bot = top - e.glyph.metrics.height * e.elementScale;
                    minY = Math.Min(minY, bot); maxY = Math.Max(maxY, top);
                    minX = Math.Min(minX, e.x); maxX = Math.Max(maxX, e.x + e.advance);
                }
            if (minY > maxY) { minX = minY = maxX = maxY = 0f; }
        }

        // ── material padding ──────────────────────────────────────────────────────

        /// <summary>
        /// Extra texels around each glyph quad so dilated / outlined / underlaid SDF shapes are
        /// not clipped: the largest material reach (in SDF units, clamped to 1) × gradient
        /// scale, plus TMP's fixed 1.25-texel margin (+4 with extra padding).
        /// </summary>
        public static float GetPadding(Material material, bool extraPadding, out float gradientScale, out float scaleRatioA)
        {
            gradientScale = 5f; scaleRatioA = 1f;
            if (material == null) return extraPadding ? 5.25f : 1.25f;
            gradientScale = material.HasProperty("_GradientScale") ? material.GetFloat("_GradientScale") : 5f;
            scaleRatioA = material.HasProperty("_ScaleRatioA") ? material.GetFloat("_ScaleRatioA") : 1f;
            float ratioC = material.HasProperty("_ScaleRatioC") ? material.GetFloat("_ScaleRatioC") : 1f;
            float faceDilate = material.GetFloat("_FaceDilate") * scaleRatioA;
            float softness = material.GetFloat("_OutlineSoftness") * scaleRatioA;
            float outline = material.GetFloat("_OutlineWidth") * scaleRatioA;
            bool mobile = material.shader != null && material.shader.name == TmpSdfShader.MobileDistanceFieldShaderName;
            if (mobile && !material.IsKeywordEnabled("OUTLINE_ON")) { outline = 0f; softness = 0f; }
            float pad = outline + softness + faceDilate;
            if (material.IsKeywordEnabled("UNDERLAY_ON") || material.IsKeywordEnabled("UNDERLAY_INNER"))
            {
                float ox = material.GetFloat("_UnderlayOffsetX") * ratioC, oy = material.GetFloat("_UnderlayOffsetY") * ratioC;
                float ud = material.GetFloat("_UnderlayDilate") * ratioC, us = material.GetFloat("_UnderlaySoftness") * ratioC;
                pad = Math.Max(pad, Math.Max(Math.Max(-ox, ox), Math.Max(-oy, oy)) + ud + us);
            }
            pad = Math.Min(Math.Max(pad, 0f), 1f) * gradientScale;
            return pad + (extraPadding ? 4f : 0f) + 1.25f;
        }
    }
}
