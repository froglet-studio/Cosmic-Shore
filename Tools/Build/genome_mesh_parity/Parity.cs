// Parity harness: runs SpindleWireRingGeometry (the runtime port) OUTSIDE Unity and diffs it against the
// shipped SpindleWireRing*.asset vertex/index buffers (the layout author_spindle_lod_meshes.py mesh_text writes:
// one stream, stride 84, uint16 indices). Built and run by run.sh. Exit 0 = every mesh matches.
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using CosmicShore.Utility;

static class Parity
{
    static byte[] Hex(string s)
    {
        var b = new byte[s.Length / 2];
        for (int i = 0; i < b.Length; i++) b[i] = byte.Parse(s.AsSpan(i * 2, 2), NumberStyles.HexNumber);
        return b;
    }

    static string Field(string text, string key)
    {
        var m = Regex.Match(text, "^\\s*" + Regex.Escape(key) + ": ?([0-9a-f]*)$", RegexOptions.Multiline);
        if (!m.Success) throw new Exception("no " + key);
        return m.Groups[1].Value;
    }

    static int Check(string repo, string name, int segments, int strands, bool negative)
    {
        string text = File.ReadAllText(Path.Combine(repo, "Assets/_Models/Spindles", name + ".asset"));
        var vb = Hex(Field(text, "_typelessdata"));
        var ib = Hex(Field(text, "m_IndexBuffer"));

        var sw = Stopwatch.StartNew();
        var r = SpindleWireRingGeometry.Build(segments, strands);
        sw.Stop();
        double first = sw.Elapsed.TotalMilliseconds;
        sw.Restart();
        const int reps = 20;
        for (int i = 0; i < reps; i++) SpindleWireRingGeometry.Build(segments, strands);
        sw.Stop();
        double warm = sw.Elapsed.TotalMilliseconds / reps;

        if (negative) r.Vertices[3 * 0 + 1] += 0.001f; // negative control: nudge vertex 0's y

        int errors = 0;
        int vc = vb.Length / 84;
        if (vc != r.VertexCount) { Console.WriteLine($"{name}: vertex count {r.VertexCount} != asset {vc}"); errors++; }
        if (ib.Length / 2 != r.Indices.Length) { Console.WriteLine($"{name}: index count {r.Indices.Length} != asset {ib.Length / 2}"); errors++; }
        int idxDiff = 0;
        for (int i = 0; i < Math.Min(ib.Length / 2, r.Indices.Length); i++)
            if (BitConverter.ToUInt16(ib, i * 2) != r.Indices[i]) idxDiff++;
        if (idxDiff > 0) { Console.WriteLine($"{name}: {idxDiff} indices differ"); errors++; }

        double maxPos = 0, maxNrm = 0, maxRest = 0; int bitDiff = 0;
        int n = Math.Min(vc, r.VertexCount);
        for (int v = 0; v < n; v++)
            for (int f = 0; f < SpindleWireRingGeometry.FloatsPerVertex; f++)
            {
                float a = BitConverter.ToSingle(vb, v * 84 + f * 4);
                float b = r.Vertices[v * SpindleWireRingGeometry.FloatsPerVertex + f];
                if (BitConverter.SingleToInt32Bits(a) != BitConverter.SingleToInt32Bits(b)) bitDiff++;
                double d = Math.Abs(a - b);
                if (f < 3) maxPos = Math.Max(maxPos, d); else if (f < 6) maxNrm = Math.Max(maxNrm, d); else maxRest = Math.Max(maxRest, d);
            }
        if (maxPos > 1e-4 || maxNrm > 1e-4 || maxRest > 1e-4) errors++;
        Console.WriteLine($"{name}{(negative ? " [negative control]" : "")}: {r.VertexCount} verts, {r.Indices.Length} indices; " +
                          $"max |d| pos {maxPos:E2} nrm {maxNrm:E2} tan/uv {maxRest:E2}; {bitDiff} floats not bit-identical; " +
                          $"build {first:F2} ms cold, {warm:F2} ms warm; asset {new FileInfo(Path.Combine(repo, "Assets/_Models/Spindles", name + ".asset")).Length} bytes " +
                          $"-> {(errors == 0 ? "MATCH" : "MISMATCH")}");
        return errors;
    }

    static int Main(string[] args)
    {
        string repo = args[0];
        int e = 0;
        e += Check(repo, "SpindleWireRing", SpindleWireRingGeometry.NearSegments, SpindleWireRingGeometry.Strands, false);
        e += Check(repo, "SpindleWireRingLow", SpindleWireRingGeometry.LowSegments, SpindleWireRingGeometry.Strands, false);
        e += Check(repo, "SpindleWireRingThread", SpindleWireRingGeometry.LowSegments, 0, false);
        int neg = Check(repo, "SpindleWireRingLow", SpindleWireRingGeometry.LowSegments, SpindleWireRingGeometry.Strands, true);
        if (neg == 0) { Console.WriteLine("negative control did NOT fire"); e++; }
        else Console.WriteLine("negative control fired (expected)");
        return e == 0 ? 0 : 1;
    }
}
