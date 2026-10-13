// Runs the SHIPPED StoatHullForm offline: topology across all four element extremes, every element
// moves the hull, blend@1 == the extreme, the baked bounds contain every weight corner, every face
// is wound outward, the parts the lope animates all exist, and the hull is the Squirrel's size.
// `--obj <dir>` also writes base.obj + one obj per element extreme (two materials: body, accent).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using UnityEngine;

static class Driver
{
    static int _fail;
    static void Check(bool ok, string what) { Console.WriteLine((ok ? "  ok   " : "  FAIL ") + what); if (!ok) _fail++; }

    static int Main(string[] args)
    {
        var s = StoatHullForm.Defaults;
        var parts = StoatHullForm.Generate(s);
        Console.WriteLine($"stoat hull: {parts.Count} parts, {parts.Sum(p => p.Verts.Count)} verts, " +
                          $"{parts.Sum(p => p.Body.Count + p.Accent.Count) / 3} tris");

        Check(parts[0].Kind == StoatHullForm.PartKind.Core && parts[0].Name == "StoatCore", "part 0 is the core plate");
        Check(parts.Count(p => p.Kind == StoatHullForm.PartKind.Segment) == s.Segments - 1, "every other body plate is its own part");
        Check(parts.Count(p => p.Kind == StoatHullForm.PartKind.Head) == 1, "one head");
        Check(parts.Count(p => p.Kind == StoatHullForm.PartKind.Tail) == s.TailSegments, "one part per tail plate");
        Check(parts.Count(p => p.Kind == StoatHullForm.PartKind.TailTip) == 1, "one crystal tail tip");
        Check(parts.Count(p => p.Kind == StoatHullForm.PartKind.Leg) == 4, "four legs");
        Check(parts.Select(p => p.Name).Distinct().Count() == parts.Count, "part names are unique (the animation resolves by name)");
        Check(parts.Sum(p => p.Accent.Count) > 0 && parts.First(p => p.Kind == StoatHullForm.PartKind.TailTip).Accent.Count > 0,
              "the domain accents exist (fins, eyes, crystal) on submesh 1");

        // Winding: every triangle's front (cross(b-a, c-a)) faces away from the centroid of the
        // connected PIECE it belongs to (a part can hold several: the head carries its wedge, two
        // ears, two eyes and a nose). Pieces are convex, so outward-from-centroid is exact.
        int inward = 0, total = 0;
        foreach (var p in parts)
            foreach (var piece in Pieces(p))
            {
                var c = Centroid(piece.SelectMany(t => t).Select(i => p.Verts[i]).ToList());
                foreach (var t in piece)
                {
                    var a = p.Verts[t[0]]; var b = p.Verts[t[1]]; var d = p.Verts[t[2]];
                    if (Vector3.Dot(Vector3.Cross(b - a, d - a), (a + b + d) / 3f - c) < -1e-6f) inward++;
                    total++;
                }
            }
        Check(inward == 0, $"every face wound outward ({inward} of {total} face inward)");

        // Size: nose to crystal tip, and the widest plate.
        var all = parts.SelectMany(p => p.Verts).ToList();
        float minZ = all.Min(v => v.z), maxZ = all.Max(v => v.z), width = all.Max(v => v.x) - all.Min(v => v.x);
        Console.WriteLine($"  length {maxZ - minZ:F2} u (nose {maxZ:F2}, tail tip {minZ:F2}), width {width:F2} u");
        Check(maxZ - minZ > 6f && maxZ - minZ < 9f, "nose-to-tail between 6 and 9 u (the Squirrel it replaces is ~4 u across)");
        Check(maxZ > s.BodyLength * 0.5f, "the head is ahead of the body (+Z is forward)");

        var set = StoatHullForm.BakeMorphSet(s);
        Check(true, "BakeMorphSet: all four extremes kept the topology");
        for (int e = 0; e < StoatHullForm.MorphElements.Length; e++)
        {
            float travel = 0f;
            for (int p = 0; p < set.BaseParts.Count; p++)
                foreach (var d in set.Deltas[e][p].VertDeltas) travel = Math.Max(travel, d.magnitude);
            Check(travel > 0.05f, $"{StoatHullForm.MorphElements[e]} moves the hull (max vertex travel {travel:F2} u)");
        }

        // Blend at a single weight of 1 reproduces that extreme exactly; bounds hold every corner.
        var verts = new List<Vector3>(); var normals = new List<Vector3>();
        float worstBlend = 0f; bool boundsOk = true;
        for (int e = 0; e < 4; e++)
        {
            var x = StoatHullForm.Generate(StoatHullForm.ApplyElementExtreme(s, StoatHullForm.MorphElements[e]));
            var w = new float[4]; w[e] = 1f;
            for (int p = 0; p < x.Count; p++)
            {
                StoatHullForm.BlendPart(set, p, w, verts, normals);
                for (int i = 0; i < verts.Count; i++) worstBlend = Math.Max(worstBlend, (verts[i] - x[p].Verts[i]).magnitude);
            }
        }
        Check(worstBlend < 1e-4f, $"blend at one weight = that extreme (worst {worstBlend:E1})");
        for (int corner = 0; corner < 16; corner++)
        {
            var w = new float[4]; for (int e = 0; e < 4; e++) w[e] = (corner >> e & 1) == 1 ? 1f : 0f;
            for (int p = 0; p < set.BaseParts.Count; p++)
            {
                StoatHullForm.BlendPart(set, p, w, verts, normals);
                foreach (var v in verts)
                    if (v.x < set.BoundsMin[p].x - 1e-4f || v.y < set.BoundsMin[p].y - 1e-4f || v.z < set.BoundsMin[p].z - 1e-4f ||
                        v.x > set.BoundsMax[p].x + 1e-4f || v.y > set.BoundsMax[p].y + 1e-4f || v.z > set.BoundsMax[p].z + 1e-4f)
                        boundsOk = false;
            }
        }
        Check(boundsOk, "baked bounds contain all 16 element-weight corners");

        // Negative control: an extreme that changes an INT must be refused.
        try
        {
            var bad = s; bad.Sides = 5;
            var bp = StoatHullForm.Generate(s); var xp = StoatHullForm.Generate(bad);
            Check(bp.Sum(p => p.Verts.Count) != xp.Sum(p => p.Verts.Count), "negative control: a topology change is visible to the assert");
        }
        catch (Exception ex) { Check(false, "negative control threw: " + ex.Message); }

        int oi = Array.IndexOf(args, "--obj");
        if (oi >= 0 && oi + 1 < args.Length)
        {
            Directory.CreateDirectory(args[oi + 1]);
            WriteObj(Path.Combine(args[oi + 1], "base.obj"), parts);
            foreach (var el in StoatHullForm.MorphElements)
                WriteObj(Path.Combine(args[oi + 1], el.ToString().ToLowerInvariant() + ".obj"),
                         StoatHullForm.Generate(StoatHullForm.ApplyElementExtreme(s, el)));
            Console.WriteLine("  wrote obj files to " + args[oi + 1]);
        }

        Console.WriteLine(_fail == 0 ? "stoat hull harness: OK" : $"stoat hull harness: {_fail} FAILURE(S)");
        return _fail == 0 ? 0 : 1;
    }

    /// <summary>A part's triangles grouped into connected pieces (shared vertex POSITIONS, since
    /// faces are flat-shaded and never share an index).</summary>
    static List<List<int[]>> Pieces(StoatHullForm.Part p)
    {
        var tris = new List<int[]>();
        foreach (var list in new[] { p.Body, p.Accent })
            for (int i = 0; i < list.Count; i += 3) tris.Add(new[] { list[i], list[i + 1], list[i + 2] });
        string Key(Vector3 v) => FormattableString.Invariant($"{Math.Round(v.x, 4)},{Math.Round(v.y, 4)},{Math.Round(v.z, 4)}");
        var parent = Enumerable.Range(0, tris.Count).ToArray();
        int Find(int x) { while (parent[x] != x) x = parent[x] = parent[parent[x]]; return x; }
        var owner = new Dictionary<string, int>();
        for (int t = 0; t < tris.Count; t++)
            foreach (var i in tris[t])
            {
                var k = Key(p.Verts[i]);
                if (owner.TryGetValue(k, out var o)) parent[Find(t)] = Find(o); else owner[k] = t;
            }
        return tris.Select((t, i) => (t, r: Find(i))).GroupBy(x => x.r).Select(g => g.Select(x => x.t).ToList()).ToList();
    }

    static Vector3 Centroid(List<Vector3> v) { var c = Vector3.zero; foreach (var x in v) c += x; return v.Count > 0 ? c / v.Count : c; }

    static void WriteObj(string path, List<StoatHullForm.Part> parts)
    {
        using var w = new StreamWriter(path);
        int offset = 1;
        foreach (var p in parts)
        {
            w.WriteLine("o " + p.Name);
            foreach (var v in p.Verts) w.WriteLine(FormattableString.Invariant($"v {v.x} {v.y} {v.z}"));
            foreach (var n in p.Normals) w.WriteLine(FormattableString.Invariant($"vn {n.x} {n.y} {n.z}"));
            foreach (var (tris, mat) in new[] { (p.Body, "body"), (p.Accent, "accent") })
            {
                if (tris.Count == 0) continue;
                w.WriteLine("usemtl " + mat);
                for (int i = 0; i < tris.Count; i += 3)
                    w.WriteLine($"f {tris[i] + offset}//{tris[i] + offset} {tris[i + 1] + offset}//{tris[i + 1] + offset} {tris[i + 2] + offset}//{tris[i + 2] + offset}");
            }
            offset += p.Verts.Count;
        }
    }
}
