using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CosmicShore.Content.Yaml
{
    /// <summary>
    /// A parsed node of Unity's serialized YAML. Unity writes a narrow, regular subset of
    /// YAML 1.1, and this reader targets exactly that subset rather than the general
    /// language: block mappings, block sequences that sit at the SAME indent as their key
    /// (<c>m_Component:\n  - component: …</c>), flow mappings/sequences that may wrap across
    /// lines, plain scalars that wrap, and single/double-quoted scalars that fold.
    /// </summary>
    public abstract class YNode
    {
        public virtual YNode this[string key] => null;
        public virtual string Scalar => null;
        public virtual IReadOnlyList<YNode> Items => Array.Empty<YNode>();

        public string Str(string key) => this[key]?.Scalar;

        /// <summary>
        /// Where this node's map entry / sequence item came from in the file it was parsed out of
        /// — set only by a source-preserving parse (<see cref="UnityYamlFile"/>). The writer
        /// copies that text verbatim while the node still hashes to what was parsed, so an
        /// untouched part of a file is written back byte for byte.
        /// </summary>
        internal SourceSpan Source;

        public float Float(string key, float fallback = 0f)
            => YScalar.TryFloat(this[key]?.Scalar, out var f) ? f : fallback;

        public int Int(string key, int fallback = 0)
            => YScalar.TryLong(this[key]?.Scalar, out var v) ? (int)v : fallback;

        public long Long(string key, long fallback = 0)
            => YScalar.TryLong(this[key]?.Scalar, out var v) ? v : fallback;

        /// <summary>Deep copy (prefab expansion mutates per-instance copies of cached source trees).</summary>
        public abstract YNode Clone();

        public bool Bool(string key, bool fallback = false)
        {
            var s = this[key]?.Scalar;
            if (s == null) return fallback;
            return s == "1" || s.Equals("true", StringComparison.OrdinalIgnoreCase);
        }
    }

    public sealed class YScalar : YNode
    {
        public readonly string Value;
        public YScalar(string value) { Value = value ?? string.Empty; }
        public override string Scalar => Value;
        public override string ToString() => Value;
        public override YNode Clone() => this; // immutable

        public static bool TryFloat(string s, out float f)
        {
            f = 0f;
            if (string.IsNullOrEmpty(s)) return false;
            if (float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out f)) return true;
            if (s == "Infinity" || s == ".inf") { f = float.PositiveInfinity; return true; }
            if (s == "-Infinity" || s == "-.inf") { f = float.NegativeInfinity; return true; }
            if (s == "NaN" || s == ".nan") { f = float.NaN; return true; }
            return false;
        }

        public static bool TryLong(string s, out long v)
        {
            v = 0;
            if (string.IsNullOrEmpty(s)) return false;
            if (long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out v)) return true;
            if (ulong.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var u)) { v = unchecked((long)u); return true; }
            if (TryFloat(s, out var f)) { v = (long)f; return true; }
            return false;
        }
    }

    public sealed class YSeq : YNode
    {
        public readonly List<YNode> List = new();

        /// <summary>Written as <c>[a, b]</c> rather than a block sequence (the parser records what it read).</summary>
        public bool Flow;

        /// <summary>The indent its block entries/items were read at (source-preserving parse only; -1 = unknown).</summary>
        internal int SourceIndent = -1;
        public override IReadOnlyList<YNode> Items => List;

        public override YNode Clone()
        {
            var c = new YSeq { Flow = Flow };
            c.List.Capacity = List.Count;
            foreach (var n in List) c.List.Add(n.Clone());
            return c;
        }
    }

    public sealed class YMap : YNode
    {
        public readonly List<KeyValuePair<string, YNode>> Entries = new();
        Dictionary<string, YNode> _index;

        /// <summary>Written as <c>{k: v, …}</c> rather than a block mapping (the parser records what it read).</summary>
        public bool Flow;

        /// <summary>The indent its block entries/items were read at (source-preserving parse only; -1 = unknown).</summary>
        internal int SourceIndent = -1;

        public void Add(string key, YNode value)
        {
            Entries.Add(new KeyValuePair<string, YNode>(key, value));
            if (_index != null) _index[key] = value;
        }

        public override YNode this[string key]
        {
            get
            {
                if (Entries.Count < 8)
                {
                    for (int i = 0; i < Entries.Count; i++)
                        if (Entries[i].Key == key) return Entries[i].Value;
                    return null;
                }
                if (_index == null)
                {
                    _index = new Dictionary<string, YNode>(Entries.Count, StringComparer.Ordinal);
                    foreach (var e in Entries) _index[e.Key] = e.Value;
                }
                return _index.TryGetValue(key, out var v) ? v : null;
            }
        }

        public bool Has(string key) => this[key] != null;

        /// <summary>Replaces the value for <paramref name="key"/>, or appends it.</summary>
        public void Set(string key, YNode value)
        {
            for (int i = 0; i < Entries.Count; i++)
            {
                if (Entries[i].Key != key) continue;
                Entries[i] = new KeyValuePair<string, YNode>(key, value);
                if (_index != null) _index[key] = value;
                return;
            }
            Add(key, value);
        }

        /// <summary>
        /// Replaces the value at <paramref name="index"/>. Write values through this (or
        /// <see cref="Set"/>), never through <see cref="Entries"/>: a map of 8+ keys answers
        /// lookups from an index, which a direct <c>Entries[i] = …</c> leaves pointing at the old value.
        /// </summary>
        public void SetAt(int index, YNode value)
        {
            var key = Entries[index].Key;
            Entries[index] = new KeyValuePair<string, YNode>(key, value);
            if (_index != null) _index[key] = value;
        }

        /// <summary>Inserts a key at <paramref name="index"/> (Unity's files fix the order of a map's keys).</summary>
        public void Insert(int index, string key, YNode value)
        {
            Entries.Insert(index, new KeyValuePair<string, YNode>(key, value));
            if (_index != null) _index[key] = value;
        }

        public bool Remove(string key)
        {
            for (int i = 0; i < Entries.Count; i++)
            {
                if (Entries[i].Key != key) continue;
                Entries.RemoveAt(i);
                _index?.Remove(key);
                return true;
            }
            return false;
        }

        public override YNode Clone()
        {
            var c = new YMap { Flow = Flow };
            c.Entries.Capacity = Entries.Count;
            foreach (var e in Entries) c.Entries.Add(new KeyValuePair<string, YNode>(e.Key, e.Value.Clone()));
            return c;
        }

        public static YMap Ref(long fileId, string guid = null, int type = 0)
        {
            var m = new YMap { Flow = true };
            m.Add("fileID", new YScalar(fileId.ToString(CultureInfo.InvariantCulture)));
            if (!string.IsNullOrEmpty(guid))
            {
                m.Add("guid", new YScalar(guid));
                m.Add("type", new YScalar(type.ToString(CultureInfo.InvariantCulture)));
            }
            return m;
        }
    }

    /// <summary>One <c>--- !u!classID &amp;fileID [stripped]</c> document.</summary>
    public sealed class UnityDocument
    {
        public int ClassId;
        public long FileId;
        public bool Stripped;
        /// <summary>The single top-level key, e.g. "GameObject", "MonoBehaviour".</summary>
        public string TypeName;
        /// <summary>The body under <see cref="TypeName"/>.</summary>
        public YMap Body;

        public override string ToString() => $"!u!{ClassId} &{FileId} {TypeName}{(Stripped ? " stripped" : "")}";

        /// <summary>The lines this document was parsed from (source-preserving parse only).</summary>
        internal DocSource Source;
    }

    /// <summary>
    /// Parser for Unity's serialized YAML files (.unity, .prefab, .asset, .mat, .meta…).
    /// </summary>
    public static class UnityYaml
    {
        /// <summary>Parses a multi-document Unity asset file.</summary>
        public static List<UnityDocument> ParseDocuments(string text) => ParseDocuments(text, null);

        /// <summary>
        /// <paramref name="src"/> non-null = a source-preserving parse: every entry and item
        /// remembers its span of <paramref name="text"/> (whose lines <paramref name="src"/> holds).
        /// </summary>
        internal static List<UnityDocument> ParseDocuments(string text, SourceText src)
        {
            var docs = new List<UnityDocument>();
            var lines = src != null ? new List<string>(src.Lines) : SplitLines(text);
            int i = 0;
            // Skip %YAML / %TAG directives.
            while (i < lines.Count && !lines[i].StartsWith("---", StringComparison.Ordinal))
                i++;
            while (i < lines.Count)
            {
                string header = lines[i];
                i++;
                int start = i;
                while (i < lines.Count && !lines[i].StartsWith("--- ", StringComparison.Ordinal) && lines[i] != "---")
                    i++;
                var doc = ParseHeader(header);
                var body = new Reader(lines, start, i, src).ParseRoot();
                if (src != null) doc.Source = new DocSource(start - 1, i);
                if (body is YMap map && map.Entries.Count > 0)
                {
                    doc.TypeName = map.Entries[0].Key;
                    doc.Body = map.Entries[0].Value as YMap ?? new YMap();
                }
                else doc.Body = new YMap();
                docs.Add(doc);
            }
            return docs;
        }

        /// <summary>
        /// Parses one YAML value as it would appear after "key: " — <c>5</c>, <c>Hello</c>,
        /// <c>{x: 0, y: 1, z: 0}</c>, <c>[]</c>, <c>'quoted: text'</c>.
        /// </summary>
        public static YNode ParseValue(string text)
            => ParseSingle("v: " + (text ?? "").Replace("\r\n", "\n").Replace("\n", "\n  "))["v"] ?? new YScalar("");

        /// <summary>Parses a single-document YAML text (e.g. a .meta file) into its root map.</summary>
        public static YMap ParseSingle(string text)
        {
            var lines = SplitLines(text);
            int start = 0;
            while (start < lines.Count && (lines[start].StartsWith("%", StringComparison.Ordinal) || lines[start].StartsWith("---", StringComparison.Ordinal)))
                start++;
            return new Reader(lines, start, lines.Count).ParseRoot() as YMap ?? new YMap();
        }

        static UnityDocument ParseHeader(string header)
        {
            var doc = new UnityDocument();
            // "--- !u!114 &11956264 stripped"
            var parts = header.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var p in parts)
            {
                if (p.StartsWith("!u!", StringComparison.Ordinal))
                    int.TryParse(p.AsSpan(3), NumberStyles.Integer, CultureInfo.InvariantCulture, out doc.ClassId);
                else if (p.StartsWith("&", StringComparison.Ordinal))
                    long.TryParse(p.AsSpan(1), NumberStyles.Integer, CultureInfo.InvariantCulture, out doc.FileId);
                else if (p == "stripped")
                    doc.Stripped = true;
            }
            return doc;
        }

        internal static List<string> SplitLines(string text)
        {
            var lines = new List<string>(text.Length / 32);
            int s = 0;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '\n')
                {
                    int e = i;
                    if (e > s && text[e - 1] == '\r') e--;
                    lines.Add(text.Substring(s, e - s));
                    s = i + 1;
                }
            }
            if (s < text.Length) lines.Add(text.Substring(s));
            return lines;
        }

        static int Indent(string line)
        {
            int n = 0;
            while (n < line.Length && line[n] == ' ') n++;
            return n;
        }

        static bool IsBlank(string line)
        {
            for (int i = 0; i < line.Length; i++)
                if (line[i] != ' ' && line[i] != '\t') return line[i] == '#';
            return true;
        }

        sealed class Reader
        {
            readonly List<string> _lines;
            int _i;
            readonly int _end;
            // Source-preserving parse only: the file's lines BEFORE the reader rewrites any
            // "- key:" line, which is the text an untouched entry is written back as.
            readonly SourceText _src;

            public Reader(List<string> lines, int start, int end, SourceText src = null) { _lines = lines; _i = start; _end = end; _src = src; }

            void Mark(YNode node, int start, string key, int indent)
            {
                if (_src != null && node != null) node.Source = new SourceSpan(_src, start, _i, key, indent);
            }

            void SkipBlank()
            {
                while (_i < _end && IsBlank(_lines[_i])) _i++;
            }

            public YNode ParseRoot()
            {
                SkipBlank();
                if (_i >= _end) return new YMap();
                return ParseBlock(Indent(_lines[_i]));
            }

            // Parses the block node whose first line is at the current position, at `indent`.
            YNode ParseBlock(int indent)
            {
                SkipBlank();
                if (_i >= _end) return new YScalar("");
                string line = _lines[_i];
                int ind = Indent(line);
                if (IsSeqItem(line, ind)) return ParseSeq(ind);
                return ParseMap(ind);
            }

            static bool IsSeqItem(string line, int ind)
                => ind < line.Length && line[ind] == '-' && (ind + 1 == line.Length || line[ind + 1] == ' ');

            YSeq ParseSeq(int indent)
            {
                var seq = new YSeq();
                if (_src != null) seq.SourceIndent = indent;
                while (true)
                {
                    SkipBlank();
                    if (_i >= _end) break;
                    string line = _lines[_i];
                    int ind = Indent(line);
                    if (ind != indent || !IsSeqItem(line, ind)) break;
                    string rest = ind + 2 <= line.Length ? line.Substring(Math.Min(line.Length, ind + 2)) : "";
                    int contentIndent = ind + 2;
                    int itemStart = _i;
                    if (rest.Length == 0)
                    {
                        _i++;
                        SkipBlank();
                        if (_i < _end && Indent(_lines[_i]) > indent) seq.List.Add(ParseBlock(Indent(_lines[_i])));
                        else seq.List.Add(new YScalar(""));
                        Mark(seq.List[^1], itemStart, null, indent);
                        continue;
                    }
                    // "- - a" opens a nested sequence whose further items sit at ind+2.
                    if (IsSeqItem(rest, 0))
                    {
                        _lines[_i] = new string(' ', contentIndent) + rest;
                        seq.List.Add(ParseSeq(contentIndent));
                        Mark(seq.List[^1], itemStart, null, indent);
                        continue;
                    }
                    // "- key: value" starts an inline map whose further keys sit at ind+2.
                    int colon = FindKeyColon(rest);
                    if (colon > 0 && rest[0] != '{' && rest[0] != '[' && rest[0] != '"' && rest[0] != '\'')
                    {
                        // Re-home the first key onto a virtual line at contentIndent.
                        _lines[_i] = new string(' ', contentIndent) + rest;
                        seq.List.Add(ParseMap(contentIndent));
                    }
                    else
                    {
                        seq.List.Add(ParseInlineValue(rest, indent));
                    }
                    Mark(seq.List[^1], itemStart, null, indent);
                }
                return seq;
            }

            YMap ParseMap(int indent)
            {
                var map = new YMap();
                if (_src != null) map.SourceIndent = indent;
                while (true)
                {
                    SkipBlank();
                    if (_i >= _end) break;
                    string line = _lines[_i];
                    int ind = Indent(line);
                    if (ind != indent || IsSeqItem(line, ind)) break;
                    string content = line.Substring(ind);
                    int colon = FindKeyColon(content);
                    if (colon < 0)
                    {
                        // Not a key line at this level (shouldn't happen in well-formed Unity YAML).
                        _i++;
                        continue;
                    }
                    string key = content.Substring(0, colon);
                    if (key.Length > 1 && (key[0] == '"' || key[0] == '\'')) key = UnquoteInline(key);
                    string rest = colon + 1 < content.Length ? content.Substring(colon + 1).TrimStart(' ') : "";
                    int entryStart = _i;
                    if (rest.Length == 0)
                    {
                        _i++;
                        SkipBlank();
                        YNode block = null;
                        if (_i < _end)
                        {
                            string next = _lines[_i];
                            int nind = Indent(next);
                            if (nind > indent) block = ParseBlock(nind);
                            else if (nind == indent && IsSeqItem(next, nind)) block = ParseSeq(nind);
                        }
                        map.Add(key, block ?? new YScalar(""));
                        Mark(map.Entries[^1].Value, entryStart, key, indent);
                        continue;
                    }
                    map.Add(key, ParseInlineValue(rest, indent));
                    Mark(map.Entries[^1].Value, entryStart, key, indent);
                }
                return map;
            }

            // A value that begins on the current line (after "key: " or "- ").
            // Consumes the current line plus any continuation lines.
            YNode ParseInlineValue(string rest, int ownerIndent)
            {
                char c = rest[0];
                if (c == '{' || c == '[')
                {
                    var sb = new StringBuilder(rest);
                    _i++;
                    while (!Balanced(sb) && _i < _end)
                    {
                        sb.Append(' ').Append(_lines[_i].TrimStart(' '));
                        _i++;
                    }
                    int p = 0;
                    return ParseFlow(sb.ToString(), ref p);
                }
                if (c == '"' || c == '\'')
                {
                    var sb = new StringBuilder(rest);
                    _i++;
                    while (!QuoteClosed(sb.ToString(), c) && _i < _end)
                    {
                        sb.Append('\n').Append(_lines[_i]);
                        _i++;
                    }
                    return new YScalar(c == '"' ? DecodeDoubleQuoted(sb.ToString()) : DecodeSingleQuoted(sb.ToString()));
                }
                if ((c == '|' || c == '>') && (rest.Length == 1 || rest[1] == '-' || rest[1] == '+' || char.IsDigit(rest[1])))
                {
                    _i++;
                    var sb = new StringBuilder();
                    int blockIndent = -1;
                    while (_i < _end)
                    {
                        string l = _lines[_i];
                        if (IsBlank(l) && l.Trim().Length == 0) { sb.Append('\n'); _i++; continue; }
                        int li = Indent(l);
                        if (li <= ownerIndent) break;
                        if (blockIndent < 0) blockIndent = li;
                        if (sb.Length > 0) sb.Append(c == '|' ? "\n" : " ");
                        sb.Append(l.Substring(Math.Min(blockIndent, l.Length)));
                        _i++;
                    }
                    return new YScalar(sb.ToString().TrimEnd('\n'));
                }
                // Plain scalar, possibly wrapped onto more-indented continuation lines.
                var plain = new StringBuilder(rest.TrimEnd());
                _i++;
                while (_i < _end)
                {
                    string l = _lines[_i];
                    if (IsBlank(l)) break;
                    int li = Indent(l);
                    if (li <= ownerIndent) break;
                    // A more-indented "key:" line would be malformed after a plain scalar,
                    // so anything deeper is continuation text.
                    plain.Append(' ').Append(l.Trim());
                    _i++;
                }
                return new YScalar(plain.ToString());
            }

            static bool Balanced(StringBuilder sb)
            {
                int depth = 0;
                bool inDq = false, inSq = false;
                for (int k = 0; k < sb.Length; k++)
                {
                    char ch = sb[k];
                    if (inDq) { if (ch == '\\') k++; else if (ch == '"') inDq = false; continue; }
                    if (inSq) { if (ch == '\'') { if (k + 1 < sb.Length && sb[k + 1] == '\'') k++; else inSq = false; } continue; }
                    if (ch == '"') inDq = true;
                    else if (ch == '\'') inSq = true;
                    else if (ch == '{' || ch == '[') depth++;
                    else if (ch == '}' || ch == ']') { depth--; if (depth == 0) return true; }
                }
                return depth == 0;
            }

            static bool QuoteClosed(string s, char q)
            {
                for (int k = 1; k < s.Length; k++)
                {
                    if (q == '"')
                    {
                        if (s[k] == '\\') { k++; continue; }
                        if (s[k] == '"') return true;
                    }
                    else if (s[k] == '\'')
                    {
                        if (k + 1 < s.Length && s[k + 1] == '\'') { k++; continue; }
                        return true;
                    }
                }
                return false;
            }
        }

        // Finds the ':' that ends a mapping key: followed by space or end-of-line, outside quotes/brackets.
        internal static int FindKeyColon(string s)
        {
            if (s.Length == 0) return -1;
            char first = s[0];
            if (first == '{' || first == '[') return -1;
            if (first == '"' || first == '\'')
            {
                // quoted key
                int k = 1;
                while (k < s.Length)
                {
                    if (first == '"' && s[k] == '\\') { k += 2; continue; }
                    if (s[k] == first)
                    {
                        if (first == '\'' && k + 1 < s.Length && s[k + 1] == '\'') { k += 2; continue; }
                        break;
                    }
                    k++;
                }
                k++;
                if (k < s.Length && s[k] == ':' && (k + 1 == s.Length || s[k + 1] == ' ')) return k;
                return -1;
            }
            for (int k = 0; k < s.Length; k++)
            {
                if (s[k] == ':' && (k + 1 == s.Length || s[k + 1] == ' ')) return k;
            }
            return -1;
        }

        static string UnquoteInline(string s)
            => s[0] == '"' ? DecodeDoubleQuoted(s) : DecodeSingleQuoted(s);

        // ── Flow collections ────────────────────────────────────────────────

        static YNode ParseFlow(string s, ref int p)
        {
            SkipWs(s, ref p);
            if (p >= s.Length) return new YScalar("");
            char c = s[p];
            if (c == '{')
            {
                p++;
                var map = new YMap { Flow = true };
                while (true)
                {
                    SkipWs(s, ref p);
                    if (p >= s.Length) break;
                    if (s[p] == '}') { p++; break; }
                    if (s[p] == ',') { p++; continue; }
                    string key = ReadFlowScalar(s, ref p, isKey: true);
                    SkipWs(s, ref p);
                    YNode value = new YScalar("");
                    if (p < s.Length && s[p] == ':')
                    {
                        p++;
                        value = ParseFlow(s, ref p);
                    }
                    map.Add(key, value);
                }
                map.Flow = map.Entries.Count > 0; // an empty {} / [] carries no style: filled, it is written as Unity writes a filled one
                return map;
            }
            if (c == '[')
            {
                p++;
                var seq = new YSeq { Flow = true };
                while (true)
                {
                    SkipWs(s, ref p);
                    if (p >= s.Length) break;
                    if (s[p] == ']') { p++; break; }
                    if (s[p] == ',') { p++; continue; }
                    seq.List.Add(ParseFlow(s, ref p));
                }
                seq.Flow = seq.List.Count > 0;
                return seq;
            }
            return new YScalar(ReadFlowScalar(s, ref p, isKey: false));
        }

        static void SkipWs(string s, ref int p)
        {
            while (p < s.Length && (s[p] == ' ' || s[p] == '\t' || s[p] == '\n')) p++;
        }

        static string ReadFlowScalar(string s, ref int p, bool isKey)
        {
            SkipWs(s, ref p);
            if (p >= s.Length) return "";
            char c = s[p];
            if (c == '"' || c == '\'')
            {
                int start = p;
                p++;
                while (p < s.Length)
                {
                    if (c == '"' && s[p] == '\\') { p += 2; continue; }
                    if (s[p] == c)
                    {
                        if (c == '\'' && p + 1 < s.Length && s[p + 1] == '\'') { p += 2; continue; }
                        break;
                    }
                    p++;
                }
                p++;
                string q = s.Substring(start, Math.Min(p, s.Length) - start);
                return c == '"' ? DecodeDoubleQuoted(q) : DecodeSingleQuoted(q);
            }
            int b = p;
            while (p < s.Length)
            {
                char ch = s[p];
                if (ch == ',' || ch == '}' || ch == ']') break;
                if (isKey && ch == ':' && (p + 1 >= s.Length || s[p + 1] == ' ')) break;
                if (!isKey && ch == ':' && p + 1 < s.Length && s[p + 1] == ' ') break;
                p++;
            }
            return s.Substring(b, p - b).Trim();
        }

        // ── Quoted scalar decoding (with YAML line folding) ─────────────────

        internal static string DecodeDoubleQuoted(string raw)
        {
            // raw includes the surrounding quotes and may contain '\n' from joined lines.
            int end = raw.LastIndexOf('"');
            if (end <= 0) end = raw.Length;
            var sb = new StringBuilder(raw.Length);
            int i = 1;
            while (i < end)
            {
                char c = raw[i];
                if (c == '\\' && i + 1 < end)
                {
                    char e = raw[i + 1];
                    i += 2;
                    switch (e)
                    {
                        case 'n': sb.Append('\n'); break;
                        case 't': sb.Append('\t'); break;
                        case 'r': sb.Append('\r'); break;
                        case '0': sb.Append('\0'); break;
                        case 'a': sb.Append('\a'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'e': sb.Append('\x1b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'v': sb.Append('\v'); break;
                        case ' ': sb.Append(' '); break;
                        case '"': sb.Append('"'); break;
                        case '/': sb.Append('/'); break;
                        case '\\': sb.Append('\\'); break;
                        case 'N': sb.Append('\u0085'); break;
                        case '_': sb.Append(' '); break;
                        case 'x': sb.Append((char)Convert.ToInt32(raw.Substring(i, 2), 16)); i += 2; break;
                        case 'u': sb.Append((char)Convert.ToInt32(raw.Substring(i, 4), 16)); i += 4; break;
                        case 'U':
                            sb.Append(char.ConvertFromUtf32(Convert.ToInt32(raw.Substring(i, 8), 16))); i += 8; break;
                        case '\n':
                            // escaped line break: join without a space, drop the next line's indentation
                            while (i < end && (raw[i] == ' ' || raw[i] == '\t')) i++;
                            break;
                        default: sb.Append(e); break;
                    }
                    continue;
                }
                if (c == '\n')
                {
                    FoldBreak(raw, ref i, end, sb);
                    continue;
                }
                sb.Append(c);
                i++;
            }
            return sb.ToString();
        }

        internal static string DecodeSingleQuoted(string raw)
        {
            int end = raw.Length - 1;
            while (end > 0 && raw[end] != '\'') end--;
            if (end <= 0) end = raw.Length;
            var sb = new StringBuilder(raw.Length);
            int i = 1;
            while (i < end)
            {
                char c = raw[i];
                if (c == '\'' && i + 1 < end && raw[i + 1] == '\'') { sb.Append('\''); i += 2; continue; }
                if (c == '\n') { FoldBreak(raw, ref i, end, sb); continue; }
                sb.Append(c);
                i++;
            }
            return sb.ToString();
        }

        // YAML flow-scalar folding: trailing spaces before a break are dropped; a single
        // break becomes a space; each additional (empty-line) break becomes '\n'.
        static void FoldBreak(string raw, ref int i, int end, StringBuilder sb)
        {
            while (sb.Length > 0 && (sb[sb.Length - 1] == ' ' || sb[sb.Length - 1] == '\t')) sb.Length--;
            int breaks = 0;
            while (i < end && (raw[i] == '\n' || raw[i] == ' ' || raw[i] == '\t'))
            {
                if (raw[i] == '\n') breaks++;
                i++;
            }
            if (breaks <= 1) sb.Append(' ');
            else sb.Append('\n', breaks - 1);
        }
    }
}
