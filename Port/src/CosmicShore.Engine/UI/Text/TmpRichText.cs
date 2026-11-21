using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CosmicShore.Engine.UI
{
    /// <summary>One token of TMP source text: a character or a rich-text tag.</summary>
    public readonly struct TmpToken
    {
        public readonly bool IsTag;
        /// <summary>Code point (characters only).</summary>
        public readonly uint Unicode;
        /// <summary>Lower-case tag name without '/' (tags only), e.g. "color".</summary>
        public readonly string TagName;
        /// <summary>Raw value after '=' (quotes stripped), or null.</summary>
        public readonly string TagValue;
        public readonly bool IsClosing;
        /// <summary>Index of the token's first character in the source string.</summary>
        public readonly int SourceIndex;

        TmpToken(bool isTag, uint unicode, string name, string value, bool closing, int src)
        { IsTag = isTag; Unicode = unicode; TagName = name; TagValue = value; IsClosing = closing; SourceIndex = src; }

        public static TmpToken Char(uint unicode, int src) => new(false, unicode, null, null, false, src);
        public static TmpToken Tag(string name, string value, bool closing, int src) => new(true, 0, name, value, closing, src);

        public override string ToString() => IsTag ? $"<{(IsClosing ? "/" : "")}{TagName}{(TagValue != null ? "=" + TagValue : "")}>" : char.ConvertFromUtf32((int)Unicode);
    }

    /// <summary>
    /// Tokenizer for TMP source text: control-character escapes (<c>\n</c>, <c>\t</c>, <c>…</c>),
    /// surrogate pairs, <c>&lt;noparse&gt;</c>, and rich-text tags. A tag the port does not
    /// recognise is emitted as literal characters, which is what TextMeshPro does with an
    /// invalid tag; recognised-but-unrendered tags (sprite, link, font, …) are consumed.
    /// Written from TMP's observable behaviour, not its source.
    /// </summary>
    public static class TmpRichText
    {
        /// <summary>Every tag the layout understands or deliberately consumes.</summary>
        public static readonly HashSet<string> KnownTags = new(StringComparer.Ordinal)
        {
            "b", "i", "u", "s", "strikethrough", "br", "color", "alpha", "size", "cspace", "mspace",
            "mark", "uppercase", "allcaps", "lowercase", "smallcaps", "sup", "sub", "voffset", "nobr",
            "noparse", "space", "pos", "align", "line-height", "indent", "margin", "width",
            // consumed without effect (no sprite / font / material switching in the port yet)
            "sprite", "link", "font", "material", "style", "gradient", "rotate", "font-weight", "page",
            "lowercase", "action", "a", "zwsp", "cr", "nbsp", "shy",
        };

        public static List<TmpToken> Tokenize(string text, bool richText, bool parseControlCharacters)
        {
            var tokens = new List<TmpToken>(text?.Length ?? 0);
            if (string.IsNullOrEmpty(text)) return tokens;
            bool noParse = false;
            int i = 0;
            while (i < text.Length)
            {
                char c = text[i];
                if (parseControlCharacters && c == '\\' && i + 1 < text.Length)
                {
                    char n = text[i + 1];
                    switch (n)
                    {
                        case 'n': tokens.Add(TmpToken.Char(10, i)); i += 2; continue;
                        case 'r': tokens.Add(TmpToken.Char(13, i)); i += 2; continue;
                        case 't': tokens.Add(TmpToken.Char(9, i)); i += 2; continue;
                        case 'v': tokens.Add(TmpToken.Char(11, i)); i += 2; continue;
                        case 'u' when i + 5 < text.Length && TryHex(text, i + 2, 4, out uint u4):
                            tokens.Add(TmpToken.Char(u4, i)); i += 6; continue;
                        case 'U' when i + 9 < text.Length && TryHex(text, i + 2, 8, out uint u8):
                            tokens.Add(TmpToken.Char(u8, i)); i += 10; continue;
                    }
                }
                if (richText && c == '<')
                {
                    int close = text.IndexOf('>', i + 1);
                    if (close > i && TryParseTag(text.Substring(i + 1, close - i - 1), out var name, out var value, out bool closing))
                    {
                        if (noParse)
                        {
                            if (closing && name == "noparse") { noParse = false; i = close + 1; continue; }
                        }
                        else if (KnownTags.Contains(name))
                        {
                            if (name == "noparse") { noParse = !closing; }
                            else if (name == "br" && !closing) tokens.Add(TmpToken.Char(10, i));
                            else if (name == "zwsp" && !closing) tokens.Add(TmpToken.Char(0x200B, i));
                            else if (name == "nbsp" && !closing) tokens.Add(TmpToken.Char(0xA0, i));
                            else if (name == "shy" && !closing) tokens.Add(TmpToken.Char(0xAD, i));
                            else if (name == "cr" && !closing) tokens.Add(TmpToken.Char(13, i));
                            else tokens.Add(TmpToken.Tag(name, value, closing, i));
                            i = close + 1;
                            continue;
                        }
                    }
                }
                if (char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                {
                    tokens.Add(TmpToken.Char((uint)char.ConvertToUtf32(c, text[i + 1]), i));
                    i += 2;
                    continue;
                }
                tokens.Add(TmpToken.Char(c, i));
                i++;
            }
            return tokens;
        }

        static bool TryHex(string s, int start, int len, out uint v)
        {
            v = 0;
            if (start + len > s.Length) return false;
            return uint.TryParse(s.AsSpan(start, len), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out v);
        }

        static bool TryParseTag(string body, out string name, out string value, out bool closing)
        {
            name = null; value = null; closing = false;
            if (string.IsNullOrEmpty(body) || body.Length > 128) return false;
            int p = 0;
            if (body[0] == '/') { closing = true; p = 1; }
            if (p < body.Length && body[p] == '#') { name = "color"; value = body.Substring(p).Trim(); return !closing; }
            int end = p;
            while (end < body.Length && body[end] != '=' && body[end] != ' ') end++;
            if (end == p) return false;
            name = body.Substring(p, end - p).ToLowerInvariant();
            if (end < body.Length && body[end] == '=')
            {
                string v = body.Substring(end + 1).Trim();
                // Only the first value matters for the tags the port renders; attributes after a space are ignored.
                if (v.Length > 0 && (v[0] == '"' || v[0] == '\''))
                {
                    char q = v[0];
                    int qe = v.IndexOf(q, 1);
                    v = qe > 0 ? v.Substring(1, qe - 1) : v.Substring(1);
                }
                else
                {
                    int sp = v.IndexOf(' ');
                    if (sp >= 0) v = v.Substring(0, sp);
                }
                value = v;
            }
            return true;
        }

        static readonly Dictionary<string, Color32> NamedColors = new(StringComparer.OrdinalIgnoreCase)
        {
            ["red"] = new Color32(255, 0, 0, 255),
            ["lightblue"] = new Color32(173, 216, 230, 255),
            ["blue"] = new Color32(0, 0, 255, 255),
            ["grey"] = new Color32(128, 128, 128, 255),
            ["black"] = new Color32(0, 0, 0, 255),
            ["green"] = new Color32(0, 255, 0, 255),
            ["white"] = new Color32(255, 255, 255, 255),
            ["orange"] = new Color32(255, 128, 0, 255),
            ["purple"] = new Color32(160, 32, 240, 255),
            ["yellow"] = new Color32(255, 255, 0, 255),
        };

        /// <summary>Parses TMP colour syntax: #RGB, #RGBA, #RRGGBB, #RRGGBBAA, or a named colour.</summary>
        public static bool TryParseColor(string value, out Color32 color)
        {
            color = new Color32(255, 255, 255, 255);
            if (string.IsNullOrEmpty(value)) return false;
            if (value[0] != '#') return NamedColors.TryGetValue(value, out color);
            string h = value.Substring(1);
            static byte H1(char c) { int v = HexVal(c); return (byte)(v * 17); }
            static byte H2(string s, int i) => (byte)(HexVal(s[i]) * 16 + HexVal(s[i + 1]));
            foreach (char ch in h) if (HexVal(ch) < 0) return false;
            switch (h.Length)
            {
                case 3: color = new Color32(H1(h[0]), H1(h[1]), H1(h[2]), 255); return true;
                case 4: color = new Color32(H1(h[0]), H1(h[1]), H1(h[2]), H1(h[3])); return true;
                case 6: color = new Color32(H2(h, 0), H2(h, 2), H2(h, 4), 255); return true;
                case 8: color = new Color32(H2(h, 0), H2(h, 2), H2(h, 4), H2(h, 6)); return true;
            }
            return false;
        }

        static int HexVal(char c)
        {
            if (c >= '0' && c <= '9') return c - '0';
            c = (char)(c | 0x20);
            if (c >= 'a' && c <= 'f') return c - 'a' + 10;
            return -1;
        }

        /// <summary>A tag length value: pixels (font units of the canvas), em, or percent.</summary>
        public enum Unit { Pixels, Em, Percent }

        public static bool TryParseLength(string value, out float number, out Unit unit, out bool relativeSign)
        {
            number = 0; unit = Unit.Pixels; relativeSign = false;
            if (string.IsNullOrWhiteSpace(value)) return false;
            string v = value.Trim();
            if (v[0] == '+' || v[0] == '-') relativeSign = true;
            if (v.EndsWith("em", StringComparison.OrdinalIgnoreCase)) { unit = Unit.Em; v = v.Substring(0, v.Length - 2); }
            else if (v.EndsWith("%", StringComparison.Ordinal)) { unit = Unit.Percent; v = v.Substring(0, v.Length - 1); }
            else if (v.EndsWith("px", StringComparison.OrdinalIgnoreCase)) { v = v.Substring(0, v.Length - 2); }
            return float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out number);
        }

        /// <summary>The visible characters of a text with tags stripped (debug / tests).</summary>
        public static string StripTags(string text, bool parseControlCharacters = true)
        {
            var sb = new StringBuilder();
            foreach (var t in Tokenize(text, true, parseControlCharacters))
                if (!t.IsTag) sb.Append(char.ConvertFromUtf32((int)t.Unicode));
            return sb.ToString();
        }
    }
}
