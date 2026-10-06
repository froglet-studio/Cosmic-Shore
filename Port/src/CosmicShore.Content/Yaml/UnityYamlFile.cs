using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CosmicShore.Content.Yaml
{
    /// <summary>The lines of a file parsed with source preservation, shared by every span into it.</summary>
    internal sealed class SourceText
    {
        public readonly string[] Lines;
        public SourceText(string[] lines) { Lines = lines; }
    }

    /// <summary>
    /// One map entry's (or sequence item's) lines in the source, plus the hash its value had
    /// when it was parsed. An entry whose value still hashes the same is written back as these
    /// exact lines; anything else is emitted canonically.
    /// </summary>
    internal sealed class SourceSpan
    {
        public readonly SourceText Text;
        public readonly int Start, End;      // [Start, End) line indices
        public readonly string Key;          // null for a sequence item
        public readonly int Indent;          // the entry's key indent / the item's "- " indent
        public ulong Hash;

        public SourceSpan(SourceText text, int start, int end, string key, int indent)
        { Text = text; Start = start; End = end; Key = key; Indent = indent; }
    }

    /// <summary>A document's header line and the header values it was parsed with.</summary>
    internal sealed class DocSource
    {
        public readonly int HeaderLine, End;
        public int ClassId;
        public long FileId;
        public bool Stripped;
        public string TypeName;
        public ulong BodyHash;
        public DocSource(int headerLine, int end) { HeaderLine = headerLine; End = end; }
    }

    /// <summary>
    /// A Unity YAML asset (.unity, .prefab, .asset, .mat, .controller, .anim …) opened for
    /// EDITING: parse it, change the <see cref="Documents"/> tree in any way (set, add or remove
    /// map entries, sequence items or whole documents), and <see cref="Write"/> it back.
    ///
    /// <para><b>Lossless by construction.</b> Every entry remembers the lines it was read from
    /// and what its value hashed to. On write, an entry whose value still hashes the same is
    /// copied verbatim; only what actually changed is re-emitted. So an unedited file comes
    /// back byte for byte, and an edited one differs from the original only where it was edited
    /// — the property that lets this port and the Unity Editor share the same files without
    /// one of them rewriting the other's formatting. Change detection is by content, not by
    /// tracking calls, so edits made directly on <c>YMap.Entries</c> / <c>YSeq.List</c> count.</para>
    ///
    /// <para><b>Canonical where it has to choose.</b> Re-emitted nodes follow the emitter Unity
    /// uses (libyaml's rules): two-space indent, sequences at their key's indent, 80-column
    /// wrapping of long scalars and flow mappings, plain scalars when YAML allows them, else
    /// single-quoted, else double-quoted with escapes. <see cref="Write"/>(canonical: true)
    /// re-emits everything, which is how the emitter's fidelity is measured.</para>
    /// </summary>
    public sealed class UnityYamlFile
    {
        public readonly List<UnityDocument> Documents;

        readonly SourceText _src;
        readonly int _preambleEnd;
        readonly bool _crlf;
        readonly bool _finalNewline;

        UnityYamlFile(List<UnityDocument> docs, SourceText src, int preambleEnd, bool crlf, bool finalNewline)
        {
            Documents = docs; _src = src; _preambleEnd = preambleEnd; _crlf = crlf; _finalNewline = finalNewline;
        }

        /// <summary>An empty file (the standard %YAML / %TAG preamble, no documents).</summary>
        public static UnityYamlFile CreateEmpty() => new(new List<UnityDocument>(), null, 0, false, true);

        public static UnityYamlFile Load(string path) => Parse(File.ReadAllText(path));

        public void Save(string path) => File.WriteAllText(path, Write(), new UTF8Encoding(false));

        public static UnityYamlFile Parse(string text)
        {
            bool crlf = text.Contains("\r\n");
            bool finalNewline = text.Length == 0 || text[^1] == '\n';
            var src = new SourceText(UnityYaml.SplitLines(text).ToArray());
            var docs = UnityYaml.ParseDocuments(text, src);
            int preambleEnd = 0;
            while (preambleEnd < src.Lines.Length && !src.Lines[preambleEnd].StartsWith("---", StringComparison.Ordinal))
                preambleEnd++;
            foreach (var d in docs)
            {
                ulong h = d.Body != null ? StampHashes(d.Body) : 0;
                if (d.Source != null)
                {
                    d.Source.ClassId = d.ClassId; d.Source.FileId = d.FileId; d.Source.Stripped = d.Stripped;
                    d.Source.TypeName = d.TypeName; d.Source.BodyHash = h;
                }
            }
            return new UnityYamlFile(docs, src, preambleEnd, crlf, finalNewline);
        }

        /// <summary>The next unused local file ID (what a new object's <c>&amp;id</c> should be).</summary>
        public long NextFileId()
        {
            long max = 0;
            foreach (var d in Documents) if (d.FileId > max) max = d.FileId;
            return max + 1;
        }

        public UnityDocument Find(long fileId)
        {
            foreach (var d in Documents) if (d.FileId == fileId) return d;
            return null;
        }

        // ── Writing ─────────────────────────────────────────────────────

        /// <summary>
        /// Serializes the documents. <paramref name="canonical"/> ignores the source and re-emits
        /// every node (a fidelity measurement; a real save keeps it false).
        /// </summary>
        public string Write(bool canonical = false)
        {
            var o = new YamlOut(canonical ? null : _src);
            if (!canonical && _src != null)
                for (int i = 0; i < _preambleEnd; i++) o.Line(_src.Lines[i]);
            else
            {
                o.Line("%YAML 1.1");
                o.Line("%TAG !u! tag:unity3d.com,2011:");
            }
            foreach (var d in Documents) WriteDocument(o, d);
            string s = o.ToString();
            if (!canonical && !_finalNewline && s.EndsWith("\n", StringComparison.Ordinal)) s = s[..^1];
            if (!canonical && _crlf) s = s.Replace("\n", "\r\n");
            return s;
        }

        void WriteDocument(YamlOut o, UnityDocument d)
        {
            var ds = o.Lossless ? d.Source : null;
            bool headerSame = ds != null && ds.ClassId == d.ClassId && ds.FileId == d.FileId && ds.Stripped == d.Stripped;
            // An untouched document is its exact source lines — including anything the reader
            // skipped as malformed (a stray tab-indented line), which no node can carry.
            if (headerSame && ds.TypeName == d.TypeName && d.Body != null && Hash(d.Body) == ds.BodyHash)
            {
                for (int i = ds.HeaderLine; i < ds.End; i++) o.Line(_src.Lines[i]);
                return;
            }
            if (headerSame)
                o.Line(_src.Lines[ds.HeaderLine]);
            else
                o.Line($"--- !u!{d.ClassId} &{d.FileId}{(d.Stripped ? " stripped" : "")}");
            o.Entry(d.TypeName ?? "MonoBehaviour", d.Body ?? new YMap(), 0, null);
        }

        /// <summary>
        /// True when both files hold the same documents (header and content), ignoring how the
        /// text is laid out. A writer's output must satisfy this against what it was given.
        /// </summary>
        public static bool SameContent(UnityYamlFile a, UnityYamlFile b)
        {
            if (a.Documents.Count != b.Documents.Count) return false;
            for (int i = 0; i < a.Documents.Count; i++)
            {
                var x = a.Documents[i]; var y = b.Documents[i];
                if (x.ClassId != y.ClassId || x.FileId != y.FileId || x.Stripped != y.Stripped || x.TypeName != y.TypeName) return false;
                if (Hash(x.Body ?? new YMap()) != Hash(y.Body ?? new YMap())) return false;
            }
            return true;
        }

        /// <summary>One value as canonical YAML text (block values span several lines).</summary>
        public static string FormatValue(YNode node)
        {
            var o = new YamlOut(null);
            o.Entry("v", node, 0, null);
            string s = o.ToString();
            return s.StartsWith("v: ", StringComparison.Ordinal) ? s[3..].TrimEnd('\n') : s[2..].TrimEnd('\n');
        }

        // ── Hashing (change detection) ──────────────────────────────────

        static ulong StampHashes(YNode n)
        {
            ulong h;
            switch (n)
            {
                case YMap m:
                    h = Mix(Fnv, m.Flow && m.Entries.Count > 0 ? 0x4du : 0x6du);
                    foreach (var e in m.Entries) { h = MixStr(h, e.Key); h = Mix(h, StampHashes(e.Value)); }
                    break;
                case YSeq q:
                    h = Mix(Fnv, q.Flow && q.List.Count > 0 ? 0x53u : 0x73u);
                    foreach (var x in q.List) h = Mix(h, StampHashes(x));
                    break;
                default:
                    h = MixStr(Mix(Fnv, 0x76u), n.Scalar);
                    break;
            }
            if (n.Source != null) n.Source.Hash = h;
            return h;
        }

        internal static ulong Hash(YNode n)
        {
            switch (n)
            {
                case YMap m:
                {
                    ulong h = Mix(Fnv, m.Flow && m.Entries.Count > 0 ? 0x4du : 0x6du);
                    foreach (var e in m.Entries) { h = MixStr(h, e.Key); h = Mix(h, Hash(e.Value)); }
                    return h;
                }
                case YSeq q:
                {
                    ulong h = Mix(Fnv, q.Flow && q.List.Count > 0 ? 0x53u : 0x73u);
                    foreach (var x in q.List) h = Mix(h, Hash(x));
                    return h;
                }
                default:
                    return MixStr(Mix(Fnv, 0x76u), n.Scalar);
            }
        }

        const ulong Fnv = 14695981039346656037UL;

        static ulong Mix(ulong h, ulong v)
        {
            // 64-bit avalanche (splitmix finalizer) so structure, not just bytes, reaches every bit.
            h ^= v + 0x9E3779B97F4A7C15UL + (h << 6) + (h >> 2);
            h ^= h >> 30; h *= 0xBF58476D1CE4E5B9UL;
            h ^= h >> 27; h *= 0x94D049BB133111EBUL;
            h ^= h >> 31;
            return h;
        }

        static ulong MixStr(ulong h, string s)
        {
            ulong f = Fnv;
            if (s != null)
                foreach (char c in s) { f ^= c; f *= 1099511628211UL; }
            return Mix(Mix(h, f), (ulong)(s?.Length ?? -1));
        }
    }

    /// <summary>
    /// The emitter: lossless copy of unchanged source spans, canonical (libyaml-rule) output
    /// for everything else. Column-tracking so the 80-column wrapping matches Unity's.
    /// </summary>
    internal sealed class YamlOut
    {
        // Measured against Unity-written scenes: a flow collection breaks once the column after a
        // ',' is PAST 80, a scalar breaks at the first space AT or past 80 (stock libyaml uses > for both).
        const int BestWidth = 80;
        const int BestIndent = 2;

        readonly StringBuilder _sb = new(1 << 16);
        readonly SourceText _src;
        int _column;
        bool _whitespace = true;
        bool _indention = true;

        public YamlOut(SourceText src) { _src = src; }

        public bool Lossless => _src != null;

        public override string ToString() => _sb.ToString();

        public void Line(string s) { _sb.Append(s).Append('\n'); _column = 0; _whitespace = true; _indention = true; }

        // ── Block structure ────────────────────────────────────────────

        static string Spaces(int n) => new(' ', n);

        /// <summary>
        /// One mapping entry at <paramref name="indent"/>. <paramref name="firstPrefix"/> replaces
        /// the indentation when the entry opens a sequence item ("  - key: …").
        /// </summary>
        public void Entry(string key, YNode value, int indent, string firstPrefix)
        {
            string prefix = firstPrefix ?? Spaces(indent);
            if (TryCopy(value, key, indent, prefix)) return;

            BeginLine(prefix);
            WriteScalarText(key, flow: false, simpleKey: true, indent);
            Put(':'); _whitespace = false;
            WriteValue(value, indent);
        }

        /// <summary>The value after "key:" or "- " (cursor is right after the indicator).</summary>
        void WriteValue(YNode value, int indent)
        {
            switch (value)
            {
                case YMap m when m.Flow || m.Entries.Count == 0:
                    WriteFlow(m, indent + BestIndent);
                    EndLine();
                    break;
                case YSeq q when q.Flow || q.List.Count == 0:
                    WriteFlow(q, indent + BestIndent);
                    EndLine();
                    break;
                case YMap m:
                {
                    EndLine();
                    int ci = Lossless && m.SourceIndent >= 0 ? m.SourceIndent : indent + BestIndent;
                    foreach (var e in m.Entries) Entry(e.Key, e.Value, ci, null);
                    break;
                }
                case YSeq q:
                {
                    EndLine();
                    int si = Lossless && q.SourceIndent >= 0 ? q.SourceIndent : indent; // indentless under a key
                    WriteBlockSeq(q, si);
                    break;
                }
                default:
                {
                    string s = value.Scalar ?? "";
                    if (s.Length > 0) WriteScalarText(s, flow: false, simpleKey: false, indent + BestIndent);
                    else Put(' '); // Unity writes an empty value as "key: "
                    EndLine();
                    break;
                }
            }
        }

        void WriteBlockSeq(YSeq q, int indent, string firstPrefix = null)
        {
            for (int n = 0; n < q.List.Count; n++)
            {
                var item = q.List[n];
                string dash = (n == 0 && firstPrefix != null ? firstPrefix : Spaces(indent)) + "- ";
                if (TryCopy(item, null, indent, dash)) continue;
                switch (item)
                {
                    case YMap m when !m.Flow && m.Entries.Count > 0:
                    {
                        int ci = Lossless && m.SourceIndent >= 0 ? m.SourceIndent : indent + BestIndent;
                        for (int i = 0; i < m.Entries.Count; i++)
                            Entry(m.Entries[i].Key, m.Entries[i].Value, ci, i == 0 ? dash.PadLeft(ci) : null);
                        break;
                    }
                    case YSeq s when !s.Flow && s.List.Count > 0:
                        WriteBlockSeq(s, indent + BestIndent, dash); // "- - a" / "  - b"
                        break;
                    case YMap or YSeq:
                        BeginLine(dash);
                        _whitespace = true;
                        WriteFlow(item, indent + BestIndent);
                        EndLine();
                        break;
                    default:
                    {
                        string s = item.Scalar ?? "";
                        if (s.Length == 0) { BeginLine(dash); EndLine(); break; }
                        BeginLine(dash);
                        _whitespace = true;
                        WriteScalarText(s, flow: false, simpleKey: false, indent + BestIndent);
                        EndLine();
                        break;
                    }
                }
            }
        }

        /// <summary>Copies the node's source lines when it is unchanged and lands in the same place.</summary>
        bool TryCopy(YNode value, string key, int indent, string firstPrefix)
        {
            if (_src == null || value?.Source is not { } s) return false;
            if (s.Text != _src || s.Key != key || s.Indent != indent) return false;
            if (s.Hash != UnityYamlFile.Hash(value)) return false;
            string first = _src.Lines[s.Start];
            int n = firstPrefix.Length;
            if (first.Length < n) return false;
            // The span's own first-line prefix is either plain indentation or "  - "; both are
            // `n` wide, so the context's prefix can be swapped in without touching the content.
            string own = first.Substring(0, n);
            if (own.TrimEnd(' ', '-').Length != 0) return false;
            _sb.Append(firstPrefix).Append(first, n, first.Length - n).Append('\n');
            for (int i = s.Start + 1; i < s.End; i++) _sb.Append(_src.Lines[i]).Append('\n');
            _column = 0; _whitespace = true; _indention = true;
            return true;
        }

        void BeginLine(string prefix)
        {
            _sb.Append(prefix);
            _column = prefix.Length;
            _whitespace = prefix.Length == 0 || prefix[^1] == ' ';
            _indention = true;
        }

        void EndLine() { _sb.Append('\n'); _column = 0; _whitespace = true; _indention = true; }

        void Put(char c) { _sb.Append(c); _column++; }

        void WriteIndent(int indent)
        {
            if (!_indention || _column > indent || (_column == indent && !_whitespace))
            { _sb.Append('\n'); _column = 0; }
            while (_column < indent) Put(' ');
            _whitespace = true;
            _indention = true;
        }

        // ── Flow collections ───────────────────────────────────────────

        void WriteFlow(YNode node, int indent)
        {
            if (!_whitespace) Put(' ');
            if (node is YMap m)
            {
                Put('{'); _whitespace = true; _indention = false;
                for (int i = 0; i < m.Entries.Count; i++)
                {
                    if (i > 0) { Put(','); _whitespace = false; }
                    if (_column > BestWidth) WriteIndent(indent);
                    WriteScalarText(m.Entries[i].Key, flow: true, simpleKey: true, indent);
                    Put(':'); _whitespace = false;
                    WriteFlowValue(m.Entries[i].Value, indent);
                }
                Put('}'); _whitespace = false;
            }
            else if (node is YSeq q)
            {
                Put('['); _whitespace = true; _indention = false;
                for (int i = 0; i < q.List.Count; i++)
                {
                    if (i > 0) { Put(','); _whitespace = false; }
                    if (_column > BestWidth) WriteIndent(indent);
                    WriteFlowValue(q.List[i], indent, afterIndicator: true);
                }
                Put(']'); _whitespace = false;
            }
        }

        void WriteFlowValue(YNode v, int indent, bool afterIndicator = false)
        {
            if (v is YMap or YSeq) { WriteFlow(v, indent + BestIndent); return; }
            string s = v.Scalar ?? "";
            if (s.Length == 0) { if (!_whitespace) Put(' '); return; } // "{class: , ns: }"
            WriteScalarText(s, flow: true, simpleKey: false, indent + BestIndent);
        }

        // ── Scalars (libyaml analyze + plain / single / double writers) ────

        void WriteScalarText(string s, bool flow, bool simpleKey, int indent)
        {
            var a = Analyze(s, simpleKey);
            char style = 'p';
            if ((flow && !a.FlowPlain) || (!flow && !a.BlockPlain)) style = '\'';
            if (s.Length == 0 && (flow || simpleKey)) style = '\'';
            if (style == '\'' && !a.Single) style = '"';
            if (simpleKey && a.Multiline) style = '"';
            bool breaks = !simpleKey;
            switch (style)
            {
                case 'p': WritePlain(s, breaks, indent); break;
                case '\'': WriteSingle(s, breaks, indent); break;
                default: WriteDouble(s, breaks, indent); break;
            }
        }

        void WritePlain(string s, bool allowBreaks, int indent)
        {
            if (!_whitespace && s.Length > 0) Put(' ');
            bool spaces = false;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == ' ')
                {
                    if (allowBreaks && !spaces && _column >= BestWidth && !(i + 1 < s.Length && s[i + 1] == ' '))
                        WriteIndent(indent);
                    else Put(c);
                    spaces = true;
                }
                else { Put(c); _indention = false; spaces = false; }
            }
            _whitespace = false; _indention = false;
        }

        void WriteSingle(string s, bool allowBreaks, int indent)
        {
            if (!_whitespace) Put(' ');
            Put('\''); _whitespace = false; _indention = false;
            bool spaces = false, brk = false;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == ' ')
                {
                    if (allowBreaks && !spaces && _column >= BestWidth && i != 0 && i != s.Length - 1 && !(i + 1 < s.Length && s[i + 1] == ' '))
                        WriteIndent(indent);
                    else Put(c);
                    spaces = true;
                }
                else if (c == '\n')
                {
                    if (!brk && c == '\n') { _sb.Append('\n'); _column = 0; }
                    _sb.Append('\n'); _column = 0; _indention = true; brk = true;
                }
                else
                {
                    if (brk) WriteIndent(indent);
                    if (c == '\'') Put('\'');
                    Put(c); _indention = false; spaces = false; brk = false;
                }
            }
            // Unity closes a value that ends in line breaks at column 0 ("'MISSION\n\n'").
            Put('\''); _whitespace = false; _indention = false;
        }

        void WriteDouble(string s, bool allowBreaks, int indent)
        {
            if (!_whitespace) Put(' ');
            Put('"'); _whitespace = false; _indention = false;
            bool spaces = false;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (!Printable(c) || c == '\uFEFF' || c == '\n' || c == '\r' || c == '"' || c == '\\' || c > 0x7E)
                {
                    Put('\\');
                    if (char.IsHighSurrogate(c) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1]))
                    {
                        foreach (char e in "U" + char.ConvertToUtf32(c, s[++i]).ToString("X8")) Put(e);
                        spaces = false; continue;
                    }
                    string esc = c switch
                    {
                        '\0' => "0", '\a' => "a", '\b' => "b", '\t' => "t", '\n' => "n", '\v' => "v",
                        '\f' => "f", '\r' => "r", '\x1b' => "e", '"' => "\"", '\\' => "\\",
                        '\u0085' => "N", '\u00A0' => "_", '\u2028' => "L", '\u2029' => "P",
                        _ when c <= 0xFF => "x" + ((int)c).ToString("X2"),
                        _ => "u" + ((int)c).ToString("X4"),
                    };
                    foreach (char e in esc) Put(e);
                    spaces = false;
                }
                else if (c == ' ')
                {
                    if (allowBreaks && !spaces && _column >= BestWidth && i != 0 && i != s.Length - 1)
                    {
                        WriteIndent(indent);
                        if (i + 1 < s.Length && s[i + 1] == ' ') Put('\\');
                    }
                    else Put(c);
                    spaces = true;
                }
                else { Put(c); spaces = false; }
            }
            Put('"'); _whitespace = false; _indention = false;
        }

        static bool Printable(char c)
            => c == '\n' || (c >= 0x20 && c <= 0x7E) || c == 0x85 || (c >= 0xA0 && c <= 0xD7FF)
            || (c >= 0xE000 && c <= 0xFFFD && c != 0xFEFF) || char.IsSurrogate(c);

        struct Analysis { public bool Multiline, FlowPlain, BlockPlain, Single; }

        static Analysis Analyze(string s, bool key)
        {
            if (s.Length == 0)
                return new Analysis { FlowPlain = false, BlockPlain = true, Single = true };
            bool flowInd = false, blockInd = false;
            if (s.StartsWith("---", StringComparison.Ordinal) || s.StartsWith("...", StringComparison.Ordinal))
                flowInd = blockInd = true;
            bool lineBreaks = false, special = false;
            bool leadSpace = false, leadBreak = false, trailSpace = false, trailBreak = false;
            bool breakSpace = false, spaceBreak = false, prevSpace = false, prevBreak = false;
            bool precededByWs = true;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                bool followedByWs = i + 1 >= s.Length || IsBlankZ(s[i + 1]);
                if (i == 0)
                {
                    if ("#,[]{}&*!|>'\"%@`".IndexOf(c) >= 0) flowInd = blockInd = true;
                    if (c == '?' || c == ':') { flowInd = true; if (followedByWs) blockInd = true; }
                    if (c == '-' && followedByWs) flowInd = blockInd = true;
                }
                else
                {
                    if (",?[]{}".IndexOf(c) >= 0) flowInd = true;
                    // Unity (unlike stock libyaml) quotes a value that ENDS in ']' —
                    // propertyPath: 'm_Materials.Array.data[0]'.
                    if (c == ']' && i == s.Length - 1 && !key) blockInd = true; // keys stay plain: m_MeshMetrics[0]: 1
                    if (c == ':') { flowInd = true; if (followedByWs) blockInd = true; }
                    if (c == '#' && precededByWs) flowInd = blockInd = true;
                }
                // Unity's emitter runs without libyaml's unicode flag: anything outside ASCII
                // is "special", so such a string is double-quoted with \u escapes.
                if (!Printable(c) || c == '\uFEFF' || c > 0x7E) special = true;
                if (c == '\n' || c == '\r') lineBreaks = true;
                if (c == ' ')
                {
                    if (i == 0) leadSpace = true;
                    if (i == s.Length - 1) trailSpace = true;
                    if (prevBreak) breakSpace = true;
                    prevSpace = true; prevBreak = false;
                }
                else if (c == '\n' || c == '\r')
                {
                    if (i == 0) leadBreak = true;
                    if (i == s.Length - 1) trailBreak = true;
                    if (prevSpace) spaceBreak = true;
                    prevBreak = true; prevSpace = false;
                }
                else { prevSpace = false; prevBreak = false; }
                precededByWs = IsBlankZ(c);
            }
            var a = new Analysis { Multiline = lineBreaks, FlowPlain = true, BlockPlain = true, Single = true };
            if (leadSpace || leadBreak || trailSpace || trailBreak) a.FlowPlain = a.BlockPlain = false;
            if (breakSpace) a.FlowPlain = a.BlockPlain = a.Single = false;
            if (spaceBreak || special) a.FlowPlain = a.BlockPlain = a.Single = false;
            if (lineBreaks) a.FlowPlain = a.BlockPlain = false;
            if (flowInd) a.FlowPlain = false;
            if (blockInd) a.BlockPlain = false;
            return a;
        }

        static bool IsBlankZ(char c) => c == ' ' || c == '\t' || c == '\n' || c == '\r' || c == '\0';
    }
}
