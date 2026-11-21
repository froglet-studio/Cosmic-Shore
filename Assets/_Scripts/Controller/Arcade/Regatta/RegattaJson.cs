using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The smallest JSON reader and writer the Regatta course genome needs, with no package behind it, so the genome
    /// and its catalogue parse the same way in Unity, in Amoebius and in the dependency-free course harness
    /// (Tools/Build/regatta_course_harness). Objects read as <see cref="Dictionary{TKey,TValue}"/> (string to object),
    /// arrays as <see cref="List{T}"/> of object, numbers as double, plus string, bool and null. The writer keeps key
    /// order and prints numbers round-trip, invariant culture.
    /// </summary>
    public static class RegattaJson
    {
        public static object Parse(string text)
        {
            if (text == null) throw new FormatException("null JSON");
            int i = 0;
            var v = ReadValue(text, ref i);
            SkipWs(text, ref i);
            if (i != text.Length) throw new FormatException($"trailing JSON at {i}");
            return v;
        }

        static void SkipWs(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        }

        static object ReadValue(string s, ref int i)
        {
            SkipWs(s, ref i);
            if (i >= s.Length) throw new FormatException("unexpected end of JSON");
            char c = s[i];
            if (c == '{') return ReadObject(s, ref i);
            if (c == '[') return ReadArray(s, ref i);
            if (c == '"') return ReadString(s, ref i);
            if (Match(s, ref i, "true")) return true;
            if (Match(s, ref i, "false")) return false;
            if (Match(s, ref i, "null")) return null;
            return ReadNumber(s, ref i);
        }

        static bool Match(string s, ref int i, string word)
        {
            if (string.CompareOrdinal(s, i, word, 0, word.Length) != 0) return false;
            i += word.Length;
            return true;
        }

        static Dictionary<string, object> ReadObject(string s, ref int i)
        {
            var d = new Dictionary<string, object>();
            i++;
            SkipWs(s, ref i);
            if (i < s.Length && s[i] == '}') { i++; return d; }
            while (true)
            {
                SkipWs(s, ref i);
                string key = ReadString(s, ref i);
                SkipWs(s, ref i);
                if (i >= s.Length || s[i] != ':') throw new FormatException($"expected ':' at {i}");
                i++;
                d[key] = ReadValue(s, ref i);
                SkipWs(s, ref i);
                if (i < s.Length && s[i] == ',') { i++; continue; }
                if (i < s.Length && s[i] == '}') { i++; return d; }
                throw new FormatException($"expected ',' or '}}' at {i}");
            }
        }

        static List<object> ReadArray(string s, ref int i)
        {
            var a = new List<object>();
            i++;
            SkipWs(s, ref i);
            if (i < s.Length && s[i] == ']') { i++; return a; }
            while (true)
            {
                a.Add(ReadValue(s, ref i));
                SkipWs(s, ref i);
                if (i < s.Length && s[i] == ',') { i++; continue; }
                if (i < s.Length && s[i] == ']') { i++; return a; }
                throw new FormatException($"expected ',' or ']' at {i}");
            }
        }

        static string ReadString(string s, ref int i)
        {
            if (i >= s.Length || s[i] != '"') throw new FormatException($"expected string at {i}");
            i++;
            var sb = new StringBuilder();
            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '"') return sb.ToString();
                if (c != '\\') { sb.Append(c); continue; }
                if (i >= s.Length) break;
                char e = s[i++];
                switch (e)
                {
                    case 'n': sb.Append('\n'); break;
                    case 't': sb.Append('\t'); break;
                    case 'r': sb.Append('\r'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'u':
                        sb.Append((char)int.Parse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                        i += 4;
                        break;
                    default: sb.Append(e); break;
                }
            }
            throw new FormatException("unterminated JSON string");
        }

        static double ReadNumber(string s, ref int i)
        {
            int start = i;
            while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
            if (i == start) throw new FormatException($"unexpected '{s[i]}' at {i}");
            return double.Parse(s.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        // ── Reading helpers ─────────────────────────────────────────────────────────────────────────────────────

        public static Dictionary<string, object> Obj(object o) => o as Dictionary<string, object>;
        public static List<object> Arr(object o) => o as List<object>;

        public static object Get(Dictionary<string, object> d, string key) =>
            d != null && d.TryGetValue(key, out var v) ? v : null;

        public static double Num(object o, double fallback)
        {
            switch (o)
            {
                case double d: return double.IsNaN(d) || double.IsInfinity(d) ? fallback : d;
                case bool b: return b ? 1 : 0;
                case string str when double.TryParse(str, NumberStyles.Float, CultureInfo.InvariantCulture, out var p): return p;
                default: return fallback;
            }
        }

        public static double Num(Dictionary<string, object> d, string key, double fallback) => Num(Get(d, key), fallback);

        public static string Str(Dictionary<string, object> d, string key, string fallback) =>
            Get(d, key) is string s ? s : fallback;

        public static bool Bool(Dictionary<string, object> d, string key, bool fallback)
        {
            var v = Get(d, key);
            return v is bool b ? b : v is double n ? n != 0 : fallback;
        }

        // ── Writing ─────────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>Writes dictionaries, lists, numbers, strings, bools and null. A <see cref="SortedDictionary{TKey,TValue}"/>
        /// or any IDictionary keeps its own key order.</summary>
        public static string Write(object value)
        {
            var sb = new StringBuilder();
            WriteValue(sb, value);
            return sb.ToString();
        }

        static void WriteValue(StringBuilder sb, object v)
        {
            switch (v)
            {
                case null: sb.Append("null"); return;
                case bool b: sb.Append(b ? "true" : "false"); return;
                case string s: WriteString(sb, s); return;
                case double d: sb.Append(FormatNumber(d)); return;
                case float f: sb.Append(FormatNumber(f)); return;
                case int n: sb.Append(n.ToString(CultureInfo.InvariantCulture)); return;
                case long l: sb.Append(l.ToString(CultureInfo.InvariantCulture)); return;
                case System.Collections.IDictionary dict:
                {
                    sb.Append('{');
                    bool first = true;
                    foreach (System.Collections.DictionaryEntry e in dict)
                    {
                        if (!first) sb.Append(", ");
                        first = false;
                        WriteString(sb, e.Key.ToString());
                        sb.Append(": ");
                        WriteValue(sb, e.Value);
                    }
                    sb.Append('}');
                    return;
                }
                case System.Collections.IEnumerable list:
                {
                    sb.Append('[');
                    bool first = true;
                    foreach (var e in list)
                    {
                        if (!first) sb.Append(", ");
                        first = false;
                        WriteValue(sb, e);
                    }
                    sb.Append(']');
                    return;
                }
                default: WriteString(sb, v.ToString()); return;
            }
        }

        /// <summary>A float32 value printed short and round-trip (what Python's float32 port prints as well).</summary>
        public static string FormatNumber(double d)
        {
            if (double.IsNaN(d) || double.IsInfinity(d)) return "0";
            float f = (float)d;
            if (f == Math.Floor(f) && Math.Abs(f) < 1e9) return ((long)f).ToString(CultureInfo.InvariantCulture);
            return f.ToString("R", CultureInfo.InvariantCulture);
        }

        static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\r': sb.Append("\\r"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }
    }
}
