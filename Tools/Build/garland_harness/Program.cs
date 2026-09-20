// Run the SHIPPED SpawnableGarland and print what it actually emits, as JSON.
//
// It measures two different kinds of thing and the second is the reason this file is not just a
// volume accumulator. COUNT and VOLUME are sums over the lay list. CLIPPING is a property of the
// whole cloud - every prism against every prism it could reach - so it cannot be derived from a
// family's own parameters and has to be measured over what the generator really emitted, in the
// orientations it really emitted them.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using UnityEngine;

public static class Program
{
    sealed class Runner : SpawnableGarland { }

    readonly struct Box
    {
        public readonly Vector3 C, X, Y, Z;   // centre + the three unit axes
        public readonly float Hx, Hy, Hz;     // half extents along them
        public readonly float Radius;         // bounding sphere, for the broadphase
        public Box(Vector3 c, Quaternion r, Vector3 s)
        {
            C = c; X = r.X; Y = r.Y; Z = r.Z;
            Hx = 0.5f * s.x; Hy = 0.5f * s.y; Hz = 0.5f * s.z;
            Radius = 0.5f * new Vector3(s.x, s.y, s.z).magnitude;
        }
    }

    /// <summary>
    /// Signed separation of two oriented boxes: the largest gap found over the 15 separating
    /// axes, so POSITIVE means clear by that much and NEGATIVE is how deep they interpenetrate.
    ///
    /// Returning the SIGNED margin rather than a bool is what makes the result a dial: a fit that
    /// merely reports "no overlaps" cannot tell a cell that clears by a hair from one that clears
    /// by a prism, and the first re-reads as clipping the moment anything moves.
    /// </summary>
    static double Radius(Box o, Vector3 axis) =>
        Math.Abs(Vector3.Dot(o.X, axis)) * o.Hx +
        Math.Abs(Vector3.Dot(o.Y, axis)) * o.Hy +
        Math.Abs(Vector3.Dot(o.Z, axis)) * o.Hz;

    static double Separation(Box a, Box b)
    {
        Vector3 d = b.C - a.C;
        double best = double.NegativeInfinity;

        double Test(Vector3 L)
        {
            double len = Math.Sqrt((double)L.x * L.x + (double)L.y * L.y + (double)L.z * L.z);
            if (len < 1e-9) return double.NegativeInfinity;   // parallel axes: the cross is void
            return (Math.Abs(Vector3.Dot(d, L)) - Radius(a, L) - Radius(b, L)) / len;
        }

        Span<Vector3> face = stackalloc Vector3[6] { a.X, a.Y, a.Z, b.X, b.Y, b.Z };
        for (int i = 0; i < 6; i++)
            best = Math.Max(best, Test(face[i]));
        for (int i = 0; i < 3; i++)
            for (int j = 3; j < 6; j++)
                best = Math.Max(best, Test(Vector3.Cross(face[i], face[j])));
        return best;
    }

    public static int Main(string[] args)
    {
        new Runner().RunBuild();
        var lays = CellEnvironmentSpawnableBase.Recorded;

        double volume = 0, nearest = double.MaxValue, farthest = 0;
        double minAxis = double.MaxValue, maxAxis = 0;
        var perDomain = new SortedDictionary<string, double>();
        var kinds = new SortedDictionary<string, int>();
        var boxes = new List<Box>(lays.Count);

        foreach (var (p, r, s, d, k) in lays)
        {
            double v = (double)s.x * s.y * s.z;
            volume += v;
            perDomain[d.ToString()] = perDomain.TryGetValue(d.ToString(), out var pv) ? pv + v : v;
            kinds[k.ToString()] = kinds.TryGetValue(k.ToString(), out var kc) ? kc + 1 : 1;

            double rad = Math.Sqrt((double)p.x * p.x + (double)p.y * p.y + (double)p.z * p.z);
            double half = 0.5 * Math.Sqrt((double)s.x * s.x + (double)s.y * s.y + (double)s.z * s.z);
            nearest = Math.Min(nearest, rad - half);
            farthest = Math.Max(farthest, rad + half);
            minAxis = Math.Min(minAxis, Math.Min(s.x, Math.Min(s.y, s.z)));
            maxAxis = Math.Max(maxAxis, Math.Max(s.x, Math.Max(s.y, s.z)));
            boxes.Add(new Box(p, r, s));
        }

        // Broadphase: a uniform hash grid sized to the largest bounding sphere, so a pair that
        // can touch always shares a cell or a face-adjacent one and the 15-axis test is only
        // ever paid for candidates.
        float cell = 0f;
        foreach (var bx in boxes) cell = Math.Max(cell, 2f * bx.Radius);
        cell = Math.Max(cell, 1f);
        var grid = new Dictionary<(int, int, int), List<int>>();
        (int, int, int) Key(Vector3 p) =>
            ((int)Math.Floor(p.x / cell), (int)Math.Floor(p.y / cell), (int)Math.Floor(p.z / cell));
        for (int i = 0; i < boxes.Count; i++)
        {
            var key = Key(boxes[i].C);
            if (!grid.TryGetValue(key, out var bucket)) grid[key] = bucket = new List<int>();
            bucket.Add(i);
        }

        int clipping = 0;
        double worst = double.PositiveInfinity;   // most negative separation found
        double tightest = double.PositiveInfinity; // smallest separation among NON-clipping pairs
        long tested = 0;
        for (int i = 0; i < boxes.Count; i++)
        {
            var (kx, ky, kz) = Key(boxes[i].C);
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        if (!grid.TryGetValue((kx + dx, ky + dy, kz + dz), out var bucket)) continue;
                        foreach (int j in bucket)
                        {
                            if (j <= i) continue;
                            // Sphere reject first - most candidates die here for two dot products.
                            Vector3 delta = boxes[j].C - boxes[i].C;
                            float sum = boxes[i].Radius + boxes[j].Radius;
                            if (delta.sqrMagnitude > sum * sum) continue;
                            tested++;
                            double sep = Separation(boxes[i], boxes[j]);
                            if (sep < 0) { clipping++; worst = Math.Min(worst, sep); }
                            else tightest = Math.Min(tightest, sep);
                        }
                    }
        }
        if (double.IsPositiveInfinity(worst)) worst = 0;
        if (double.IsPositiveInfinity(tightest)) tightest = 0;

        var inv2 = CultureInfo.InvariantCulture;
        string Num(double d) => d.ToString("R", inv2);
        var sb = new System.Text.StringBuilder();
        sb.Append("{\n");
        sb.Append($"  \"count\": {lays.Count},\n");
        sb.Append($"  \"volume\": {Num(volume)},\n");
        sb.Append($"  \"nearest\": {Num(nearest)},\n");
        sb.Append($"  \"farthest\": {Num(farthest)},\n");
        sb.Append($"  \"min_axis\": {Num(minAxis)},\n");
        sb.Append($"  \"max_axis\": {Num(maxAxis)},\n");
        sb.Append($"  \"clipping_pairs\": {clipping},\n");
        sb.Append($"  \"worst_penetration\": {Num(-worst)},\n");
        sb.Append($"  \"tightest_clearance\": {Num(tightest)},\n");
        sb.Append($"  \"narrowphase_tests\": {tested},\n");
        sb.Append("  \"per_domain\": {" +
            string.Join(", ", perDomain.Select(kv => $"\"{kv.Key}\": {Num(kv.Value)}")) + "},\n");
        sb.Append("  \"kinds\": {" +
            string.Join(", ", kinds.Select(kv => $"\"{kv.Key}\": {kv.Value}")) + "}\n");
        sb.Append("}\n");
        Console.Write(sb.ToString());
        return 0;
    }
}
