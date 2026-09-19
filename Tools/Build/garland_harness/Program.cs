// Run the SHIPPED SpawnableGarland and print what it actually emits, as JSON.
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

    public static int Main(string[] args)
    {
        new Runner().RunBuild();
        var lays = CellEnvironmentSpawnableBase.Recorded;

        double volume = 0, nearest = double.MaxValue, farthest = 0;
        double minAxis = double.MaxValue, maxAxis = 0;
        var perDomain = new SortedDictionary<string, double>();
        var kinds = new SortedDictionary<string, int>();

        foreach (var (p, s, d, k) in lays)
        {
            double v = (double)s.x * s.y * s.z;
            volume += v;
            perDomain[d.ToString()] = perDomain.TryGetValue(d.ToString(), out var pv) ? pv + v : v;
            kinds[k.ToString()] = kinds.TryGetValue(k.ToString(), out var kc) ? kc + 1 : 1;

            double r = Math.Sqrt((double)p.x * p.x + (double)p.y * p.y + (double)p.z * p.z);
            double half = 0.5 * Math.Sqrt((double)s.x * s.x + (double)s.y * s.y + (double)s.z * s.z);
            nearest = Math.Min(nearest, r - half);
            farthest = Math.Max(farthest, r + half);
            minAxis = Math.Min(minAxis, Math.Min(s.x, Math.Min(s.y, s.z)));
            maxAxis = Math.Max(maxAxis, Math.Max(s.x, Math.Max(s.y, s.z)));
        }

        var inv = CultureInfo.InvariantCulture;
        string Num(double d) => d.ToString("R", inv);
        var sb = new System.Text.StringBuilder();
        sb.Append("{\n");
        sb.Append($"  \"count\": {lays.Count},\n");
        sb.Append($"  \"volume\": {Num(volume)},\n");
        sb.Append($"  \"nearest\": {Num(nearest)},\n");
        sb.Append($"  \"farthest\": {Num(farthest)},\n");
        sb.Append($"  \"min_axis\": {Num(minAxis)},\n");
        sb.Append($"  \"max_axis\": {Num(maxAxis)},\n");
        sb.Append("  \"per_domain\": {" +
            string.Join(", ", perDomain.Select(kv => $"\"{kv.Key}\": {Num(kv.Value)}")) + "},\n");
        sb.Append("  \"kinds\": {" +
            string.Join(", ", kinds.Select(kv => $"\"{kv.Key}\": {kv.Value}")) + "}\n");
        sb.Append("}\n");
        Console.Write(sb.ToString());
        return 0;
    }
}
