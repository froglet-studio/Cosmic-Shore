using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace CosmicShore.Content.Models
{
    /// <summary>
    /// Pure-C# FBX node-tree reader: binary 7.x (7100..7400 with 32-bit record offsets,
    /// 7500+ with 64-bit ones; zlib-deflated arrays) and ASCII 7.x (the two icosahedron
    /// FX meshes in the project are ASCII 7.5). Produces the same <see cref="FbxNode"/>
    /// tree for both, so everything above it is encoding-agnostic.
    /// </summary>
    public static class FbxReader
    {
        static readonly byte[] BinaryMagic = Encoding.ASCII.GetBytes("Kaydara FBX Binary  \0");

        public sealed class Result
        {
            /// <summary>Top-level records (FBXHeaderExtension, GlobalSettings, Definitions, Objects, Connections, Takes…).</summary>
            public readonly List<FbxNode> Nodes = new();
            public int Version;
            public bool Binary;

            public FbxNode Top(string name)
            {
                foreach (var n in Nodes) if (n.Name == name) return n;
                return null;
            }
        }

        public static Result ReadFile(string path) => Read(File.ReadAllBytes(path));

        public static Result Read(byte[] data)
        {
            if (IsBinary(data)) return ReadBinary(data);
            return ReadAscii(Encoding.UTF8.GetString(data));
        }

        public static bool IsBinary(byte[] data)
        {
            if (data.Length < 27) return false;
            for (int i = 0; i < BinaryMagic.Length; i++) if (data[i] != BinaryMagic[i]) return false;
            return true;
        }

        // ── Binary ─────────────────────────────────────────────────────────

        static Result ReadBinary(byte[] data)
        {
            var res = new Result { Binary = true, Version = BitConverter.ToInt32(data, 23) };
            bool wide = res.Version >= 7500;
            long pos = 27;
            while (pos < data.Length)
            {
                var node = ReadBinaryNode(data, ref pos, wide);
                if (node == null) break; // null record terminates the top level
                res.Nodes.Add(node);
            }
            return res;
        }

        static FbxNode ReadBinaryNode(byte[] d, ref long pos, bool wide)
        {
            long endOffset, numProps, propLen;
            if (wide)
            {
                if (pos + 25 > d.Length) return null;
                endOffset = (long)BitConverter.ToUInt64(d, (int)pos);
                numProps = (long)BitConverter.ToUInt64(d, (int)pos + 8);
                propLen = (long)BitConverter.ToUInt64(d, (int)pos + 16);
                pos += 24;
            }
            else
            {
                if (pos + 13 > d.Length) return null;
                endOffset = BitConverter.ToUInt32(d, (int)pos);
                numProps = BitConverter.ToUInt32(d, (int)pos + 4);
                propLen = BitConverter.ToUInt32(d, (int)pos + 8);
                pos += 12;
            }
            int nameLen = d[pos++];
            if (endOffset == 0 && numProps == 0 && propLen == 0 && nameLen == 0) return null;

            var node = new FbxNode(Encoding.UTF8.GetString(d, (int)pos, nameLen));
            pos += nameLen;

            long propsStart = pos;
            for (long p = 0; p < numProps; p++) node.Props.Add(ReadProperty(d, ref pos));
            pos = propsStart + propLen;

            while (pos < endOffset)
            {
                var child = ReadBinaryNode(d, ref pos, wide);
                if (child == null) break;
                node.Children.Add(child);
            }
            pos = endOffset;
            return node;
        }

        static object ReadProperty(byte[] d, ref long pos)
        {
            char type = (char)d[pos++];
            int p = (int)pos;
            switch (type)
            {
                case 'Y': pos += 2; return BitConverter.ToInt16(d, p);
                case 'C': pos += 1; return d[p] != 0;
                case 'I': pos += 4; return BitConverter.ToInt32(d, p);
                case 'F': pos += 4; return BitConverter.ToSingle(d, p);
                case 'D': pos += 8; return BitConverter.ToDouble(d, p);
                case 'L': pos += 8; return BitConverter.ToInt64(d, p);
                case 'S':
                {
                    int len = BitConverter.ToInt32(d, p);
                    pos += 4 + len;
                    return Encoding.UTF8.GetString(d, p + 4, len);
                }
                case 'R':
                {
                    int len = BitConverter.ToInt32(d, p);
                    pos += 4 + len;
                    var raw = new byte[len];
                    Array.Copy(d, p + 4, raw, 0, len);
                    return raw;
                }
                case 'f': case 'd': case 'l': case 'i': case 'b':
                {
                    int count = BitConverter.ToInt32(d, p);
                    int encoding = BitConverter.ToInt32(d, p + 4);
                    int compressedLen = BitConverter.ToInt32(d, p + 8);
                    pos += 12 + compressedLen;
                    int elem = type switch { 'f' => 4, 'd' => 8, 'l' => 8, 'i' => 4, _ => 1 };
                    byte[] bytes;
                    int offset;
                    if (encoding == 1)
                    {
                        bytes = new byte[count * elem];
                        using var ms = new MemoryStream(d, p + 12, compressedLen);
                        using var z = new ZLibStream(ms, CompressionMode.Decompress);
                        int read = 0;
                        while (read < bytes.Length)
                        {
                            int n = z.Read(bytes, read, bytes.Length - read);
                            if (n <= 0) break;
                            read += n;
                        }
                        offset = 0;
                    }
                    else { bytes = d; offset = p + 12; }
                    switch (type)
                    {
                        case 'f': { var a = new float[count]; Buffer.BlockCopy(bytes, offset, a, 0, count * 4); return a; }
                        case 'd': { var a = new double[count]; Buffer.BlockCopy(bytes, offset, a, 0, count * 8); return a; }
                        case 'l': { var a = new long[count]; Buffer.BlockCopy(bytes, offset, a, 0, count * 8); return a; }
                        case 'i': { var a = new int[count]; Buffer.BlockCopy(bytes, offset, a, 0, count * 4); return a; }
                        default: { var a = new bool[count]; for (int k = 0; k < count; k++) a[k] = bytes[offset + k] != 0; return a; }
                    }
                }
                default:
                    throw new InvalidDataException($"FBX: unknown property type '{type}' at offset {pos - 1}");
            }
        }

        // ── ASCII ──────────────────────────────────────────────────────────

        static Result ReadAscii(string text)
        {
            var res = new Result { Binary = false };
            var lex = new Lexer(text);
            var root = new FbxNode("");
            ParseAsciiChildren(lex, root, topLevel: true);
            res.Nodes.AddRange(root.Children);
            var header = res.Top("FBXHeaderExtension");
            res.Version = (int)(header?.ChildLong("FBXVersion") ?? 0);
            if (res.Version == 0)
            {
                // "; FBX 7.5.0 project file"
                int i = text.IndexOf("FBX ", StringComparison.Ordinal);
                if (i >= 0 && i + 9 <= text.Length && int.TryParse(text.Substring(i + 4, 5).Replace(".", ""), out var v)) res.Version = v * 10;
            }
            return res;
        }

        enum Tok { End, Key, Str, Num, Comma, Open, Close, Star }

        sealed class Lexer
        {
            readonly string _s;
            int _i;
            public Tok Kind;
            public string Text;
            bool _peeked;

            public Lexer(string s) { _s = s; }

            public Tok Peek() { if (!_peeked) { Advance(); _peeked = true; } return Kind; }
            public Tok Next() { if (_peeked) { _peeked = false; return Kind; } Advance(); return Kind; }

            void Advance()
            {
                while (_i < _s.Length)
                {
                    char c = _s[_i];
                    if (c == ';') { while (_i < _s.Length && _s[_i] != '\n') _i++; continue; }
                    if (char.IsWhiteSpace(c)) { _i++; continue; }
                    break;
                }
                if (_i >= _s.Length) { Kind = Tok.End; Text = null; return; }
                char ch = _s[_i];
                switch (ch)
                {
                    case ',': _i++; Kind = Tok.Comma; return;
                    case '{': _i++; Kind = Tok.Open; return;
                    case '}': _i++; Kind = Tok.Close; return;
                    case '*': _i++; Kind = Tok.Star; return;
                    case '"':
                    {
                        int start = ++_i;
                        while (_i < _s.Length && _s[_i] != '"') _i++;
                        Text = _s.Substring(start, _i - start);
                        _i++;
                        Kind = Tok.Str;
                        return;
                    }
                }
                int b = _i;
                while (_i < _s.Length)
                {
                    char c = _s[_i];
                    if (char.IsWhiteSpace(c) || c == ',' || c == '{' || c == '}' || c == '"' || c == ';') break;
                    if (c == ':') { _i++; Text = _s.Substring(b, _i - b - 1); Kind = Tok.Key; return; }
                    _i++;
                }
                Text = _s.Substring(b, _i - b);
                Kind = Tok.Num;
            }
        }

        static void ParseAsciiChildren(Lexer lex, FbxNode parent, bool topLevel)
        {
            while (true)
            {
                var t = lex.Next();
                if (t == Tok.End) return;
                if (t == Tok.Close) { if (!topLevel) return; continue; }
                if (t != Tok.Key) continue;
                var node = new FbxNode(lex.Text);
                parent.Children.Add(node);
                ParseAsciiNodeBody(lex, node);
            }
        }

        static void ParseAsciiNodeBody(Lexer lex, FbxNode node)
        {
            // Values until end of line-equivalent: a Key token, '{', '}' or End.
            while (true)
            {
                var t = lex.Peek();
                switch (t)
                {
                    case Tok.Str: lex.Next(); node.Props.Add(lex.Text); break;
                    case Tok.Num: lex.Next(); node.Props.Add(ParseNumber(lex.Text)); break;
                    case Tok.Comma: lex.Next(); break;
                    case Tok.Star:
                    {
                        lex.Next();
                        lex.Next(); // count
                        // "{ a: v,v,v }"
                        if (lex.Next() != Tok.Open) return;
                        var values = new List<string>();
                        while (true)
                        {
                            var k = lex.Next();
                            if (k == Tok.Close || k == Tok.End) break;
                            if (k == Tok.Num) values.Add(lex.Text);
                        }
                        node.Props.Add(ToArray(values));
                        return;
                    }
                    case Tok.Open:
                        lex.Next();
                        ParseAsciiChildren(lex, node, topLevel: false);
                        return;
                    default:
                        return; // Key / Close / End: the next record begins (Close consumed by caller)
                }
            }
        }

        static object ParseNumber(string s)
        {
            if (s.Length == 1 && (s[0] == 'T' || s[0] == 'Y')) return true;
            if (s.Length == 1 && (s[0] == 'F' || s[0] == 'N')) return false;
            if (long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l)) return l;
            if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) return d;
            return s;
        }

        static object ToArray(List<string> values)
        {
            bool integral = true;
            foreach (var v in values)
                if (v.IndexOf('.') >= 0 || v.IndexOf('e') >= 0 || v.IndexOf('E') >= 0) { integral = false; break; }
            if (integral)
            {
                var a = new long[values.Count];
                for (int i = 0; i < a.Length; i++) long.TryParse(values[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out a[i]);
                return a;
            }
            var r = new double[values.Count];
            for (int i = 0; i < r.Length; i++) double.TryParse(values[i], NumberStyles.Float, CultureInfo.InvariantCulture, out r[i]);
            return r;
        }
    }
}
