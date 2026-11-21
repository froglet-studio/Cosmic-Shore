using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CosmicShore.Content.Models
{
    /// <summary>
    /// One record of an FBX node tree (binary or ASCII): a name, an ordered property list,
    /// and nested child records. Property values are boxed as the FBX primitive they were
    /// stored as: <c>short</c> (Y), <c>bool</c> (C), <c>int</c> (I), <c>float</c> (F),
    /// <c>double</c> (D), <c>long</c> (L), <c>byte[]</c> (R), <c>string</c> (S) and the
    /// arrays <c>float[]</c> (f), <c>double[]</c> (d), <c>long[]</c> (l), <c>int[]</c> (i),
    /// <c>bool[]</c> (b). ASCII files produce the same shapes (numbers become long/double,
    /// <c>*N { a: ... }</c> arrays become double[] or long[]).
    /// </summary>
    public sealed class FbxNode
    {
        public string Name;
        public readonly List<object> Props = new();
        public readonly List<FbxNode> Children = new();

        public FbxNode(string name) { Name = name; }

        public FbxNode Child(string name)
        {
            foreach (var c in Children) if (c.Name == name) return c;
            return null;
        }

        public IEnumerable<FbxNode> ChildrenNamed(string name)
        {
            foreach (var c in Children) if (c.Name == name) yield return c;
        }

        public object Prop(int i) => i < Props.Count ? Props[i] : null;

        public long Long(int i) => ToLong(Prop(i));
        public double Double(int i) => ToDouble(Prop(i));
        public string String(int i) => ToStr(Prop(i));

        /// <summary>The first property of child <paramref name="name"/> as a number array (any numeric FBX array type).</summary>
        public double[] DoubleArray(string name) => ToDoubleArray(Child(name)?.Prop(0));
        public int[] IntArray(string name) => ToIntArray(Child(name)?.Prop(0));
        public long[] LongArray(string name) => ToLongArray(Child(name)?.Prop(0));

        /// <summary>First scalar property of child <paramref name="name"/> as a string (e.g. <c>MappingInformationType</c>).</summary>
        public string ChildString(string name) => ToStr(Child(name)?.Prop(0));
        public long ChildLong(string name, long fallback = 0) => Child(name) is { } c && c.Props.Count > 0 ? ToLong(c.Prop(0)) : fallback;
        public double ChildDouble(string name, double fallback = 0) => Child(name) is { } c && c.Props.Count > 0 ? ToDouble(c.Prop(0)) : fallback;

        public static long ToLong(object o) => o switch
        {
            long l => l, int i => i, short s => s, bool b => b ? 1 : 0, double d => (long)d, float f => (long)f,
            string str when long.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) => v,
            _ => 0,
        };

        public static double ToDouble(object o) => o switch
        {
            double d => d, float f => f, long l => l, int i => i, short s => s, bool b => b ? 1 : 0,
            string str when double.TryParse(str, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) => v,
            _ => 0,
        };

        public static string ToStr(object o) => o switch
        {
            null => null,
            string s => s,
            byte[] b => Encoding.UTF8.GetString(b),
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            _ => o.ToString(),
        };

        public static double[] ToDoubleArray(object o)
        {
            switch (o)
            {
                case double[] d: return d;
                case float[] f: { var r = new double[f.Length]; for (int i = 0; i < f.Length; i++) r[i] = f[i]; return r; }
                case long[] l: { var r = new double[l.Length]; for (int i = 0; i < l.Length; i++) r[i] = l[i]; return r; }
                case int[] n: { var r = new double[n.Length]; for (int i = 0; i < n.Length; i++) r[i] = n[i]; return r; }
                case bool[] b: { var r = new double[b.Length]; for (int i = 0; i < b.Length; i++) r[i] = b[i] ? 1 : 0; return r; }
                default: return Array.Empty<double>();
            }
        }

        public static int[] ToIntArray(object o)
        {
            switch (o)
            {
                case int[] n: return n;
                case long[] l: { var r = new int[l.Length]; for (int i = 0; i < l.Length; i++) r[i] = (int)l[i]; return r; }
                case double[] d: { var r = new int[d.Length]; for (int i = 0; i < d.Length; i++) r[i] = (int)d[i]; return r; }
                case float[] f: { var r = new int[f.Length]; for (int i = 0; i < f.Length; i++) r[i] = (int)f[i]; return r; }
                case bool[] b: { var r = new int[b.Length]; for (int i = 0; i < b.Length; i++) r[i] = b[i] ? 1 : 0; return r; }
                default: return Array.Empty<int>();
            }
        }

        public static long[] ToLongArray(object o)
        {
            switch (o)
            {
                case long[] l: return l;
                case int[] n: { var r = new long[n.Length]; for (int i = 0; i < n.Length; i++) r[i] = n[i]; return r; }
                case double[] d: { var r = new long[d.Length]; for (int i = 0; i < d.Length; i++) r[i] = (long)d[i]; return r; }
                default: return Array.Empty<long>();
            }
        }

        public override string ToString() => $"{Name} ({Props.Count} props, {Children.Count} children)";
    }
}
