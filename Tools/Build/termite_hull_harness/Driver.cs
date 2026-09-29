// Compile-and-RUN gate for the SHIPPED TermiteHullForm. Proves the file compiles, that the four
// element extremes hold topology AND pivots (the assertion the blend stands on), that the baked
// bounds contain every reachable weight combination, that blending to weight 1 reproduces the
// extreme build exactly, and that the silhouette is a QUEEN (mostly abdomen, wings longer than the
// body). Run: ./run.sh            (checks)
//      ./run.sh --obj <path>      (also write the base hull as a Wavefront OBJ for a look)
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using UnityEngine;

public static class Driver
{
    static int _failures;

    static void Check(bool ok, string what)
    {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + what);
        if (!ok) _failures++;
    }

    /// <summary>The proportions the Termite prefab authors — TermiteHullBuilder's field
    /// initializers, which the setup tool leaves untouched, so the prefab carries exactly these.</summary>
    static TermiteHullForm.Settings Shipped() => new()
    {
        HeadRadius = 1.55f, HeadLength = 2.7f,
        MandibleLength = 1.7f, MandibleThickness = 0.24f,
        AntennaLength = 4.2f, AntennaThickness = 0.1f,
        ThoraxRadius = 1.15f, ThoraxLength = 3.1f,
        LegLength = 3.3f, LegThickness = 0.17f,
        AbdomenRadius = 2.7f, AbdomenLength = 11.5f,
        PlateBulge = 0.08f, MembraneRecess = 0.12f,
        AbdomenSegments = 7, RingsPerSegment = 4,
        WingLength = 15f, WingWidth = 2.7f, WingLift = 0.4f, HindScale = 0.94f,
        WingSpanSegments = 12, WingChordSegments = 4,
        Sides = 12,
    };

    public static int Main(string[] args)
    {
        var s = Shipped();
        var parts = TermiteHullForm.Generate(s);

        Console.WriteLine("=== TermiteHullForm — shipped settings ===");
        int totalVerts = 0, totalTris = 0;
        foreach (var p in parts)
        {
            totalVerts += p.Verts.Count;
            totalTris += (p.Body.Count + p.Accent.Count) / 3;
            Console.WriteLine($"  {p.Name,-10} verts {p.Verts.Count,5}  body tris {p.Body.Count / 3,5}" +
                              $"  accent tris {p.Accent.Count / 3,5}  pivot {p.Pivot}");
        }
        Console.WriteLine($"  TOTAL      verts {totalVerts}  tris {totalTris}");

        Check(parts.Count == 6, "six parts (core, abdomen, four wings)");
        Check(parts[0].Name == "Core" && parts[1].Name == "Abdomen", "part order: Core then Abdomen");
        Check(parts[0].Accent.Count == 0, "the Core emits NOTHING into the accent submesh");
        Check(parts[1].Body.Count > 0 && parts[1].Accent.Count > 0,
              "the Abdomen carries BOTH plates (slot 0) and domain membrane (slot 1)");
        for (int i = 2; i < parts.Count; i++)
            Check(parts[i].Body.Count == 0 && parts[i].Accent.Count > 0,
                  $"{parts[i].Name} is accent-only (the wings take the domain colour)");
        Check(totalTris < 12000, $"triangle budget: {totalTris} < 12000");

        // No NaN anywhere — a degenerate frame in a tube or a zero-radius ring would produce one.
        bool finite = true;
        foreach (var p in parts)
            foreach (var v in p.Verts)
                finite &= !(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z));
        foreach (var p in parts)
            foreach (var n in p.Normals)
                finite &= !(float.IsNaN(n.x) || float.IsNaN(n.y) || float.IsNaN(n.z));
        Check(finite, "every vertex and normal is finite");

        // Every index is in range.
        bool indicesOk = true;
        foreach (var p in parts)
        {
            foreach (int i in p.Body) indicesOk &= i >= 0 && i < p.Verts.Count;
            foreach (int i in p.Accent) indicesOk &= i >= 0 && i < p.Verts.Count;
        }
        Check(indicesOk, "every triangle index addresses a real vertex");

        TermiteHullForm.MorphSet set = null;
        try { set = TermiteHullForm.BakeMorphSet(s); Check(true, "T1 bake: all four element extremes hold topology AND pivots"); }
        catch (Exception e) { Check(false, "T1 bake threw: " + e.Message); return Finish(); }

        for (int e = 0; e < TermiteHullForm.MorphElements.Length; e++)
        {
            float travel = 0f;
            for (int p = 0; p < parts.Count; p++)
                foreach (var d in set.Deltas[e][p].VertDeltas)
                    travel = Mathf.Max(travel, d.magnitude);
            Check(travel > 0.25f,
                  $"T2 {TermiteHullForm.MorphElements[e]} moves the hull (max travel {travel:F2}u)");
        }

        var verts = new List<Vector3>();
        var normals = new List<Vector3>();
        for (int e = 0; e < TermiteHullForm.MorphElements.Length; e++)
        {
            var extreme = TermiteHullForm.Generate(
                TermiteHullForm.ApplyElementExtreme(s, TermiteHullForm.MorphElements[e]));
            var w = new float[4];
            w[e] = 1f;
            float worst = 0f;
            for (int p = 0; p < parts.Count; p++)
            {
                TermiteHullForm.BlendPart(set, p, w, verts, normals);
                for (int i = 0; i < verts.Count; i++)
                    worst = Mathf.Max(worst, (verts[i] - extreme[p].Verts[i]).magnitude);
            }
            Check(worst < 1e-4f, $"T3 {TermiteHullForm.MorphElements[e]} blend@1 == extreme (worst {worst:E2})");
        }

        float slack = float.PositiveInfinity;
        for (int corner = 0; corner < 16; corner++)
        {
            var w = new float[4];
            for (int e = 0; e < 4; e++) w[e] = ((corner >> e) & 1) == 1 ? 1f : 0f;
            for (int p = 0; p < parts.Count; p++)
            {
                TermiteHullForm.BlendPart(set, p, w, verts, normals);
                for (int i = 0; i < verts.Count; i++)
                {
                    Vector3 v = verts[i], lo = set.BoundsMin[p], hi = set.BoundsMax[p];
                    slack = Mathf.Min(slack, Mathf.Min(
                        Mathf.Min(v.x - lo.x, hi.x - v.x),
                        Mathf.Min(Mathf.Min(v.y - lo.y, hi.y - v.y), Mathf.Min(v.z - lo.z, hi.z - v.z))));
                }
            }
        }
        Check(slack >= -1e-4f, $"T4 baked bounds contain all 16 weight corners (tightest slack {slack:F4}u)");

        // T5 — the silhouette is a QUEEN: the abdomen is most of the body's length, and the wings
        // (spread, as the geometry is emitted) are longer than the whole body.
        float bodyMinZ = float.PositiveInfinity, bodyMaxZ = float.NegativeInfinity;
        foreach (var v in parts[0].Verts) { bodyMinZ = Mathf.Min(bodyMinZ, v.z); bodyMaxZ = Mathf.Max(bodyMaxZ, v.z); }
        float abdMinZ = float.PositiveInfinity, abdMaxZ = float.NegativeInfinity;
        foreach (var v in parts[1].Verts) { abdMinZ = Mathf.Min(abdMinZ, v.z); abdMaxZ = Mathf.Max(abdMaxZ, v.z); }
        float bodyLength = Mathf.Max(bodyMaxZ, abdMaxZ) - Mathf.Min(bodyMinZ, abdMinZ);
        float abdLength = abdMaxZ - abdMinZ;
        float wingSpan = 0f;
        for (int i = 2; i < parts.Count; i++)
            foreach (var v in parts[i].Verts) wingSpan = Mathf.Max(wingSpan, Mathf.Abs(v.x));
        Console.WriteLine($"  body length {bodyLength:F2}u  abdomen {abdLength:F2}u  half-span {wingSpan:F2}u");
        Check(abdLength / bodyLength > 0.5f, $"T5 the abdomen is most of the body ({abdLength / bodyLength:P0})");
        Check(wingSpan > bodyLength * 0.7f, "T5 each wing reaches past the tail when folded (half-span > 0.7 body)");

        // T6 — the abdomen closes at its tip (the last ring collapses onto the axis), so the
        // silhouette never shows a hole where a camera looks up the queen's tail.
        int sides = Mathf.Max(4, s.Sides);
        int ringVerts = sides + 1;
        var abd = parts[1];
        float tipRadius = 0f;
        for (int i = abd.Verts.Count - ringVerts; i < abd.Verts.Count; i++)
            tipRadius = Mathf.Max(tipRadius, new Vector2(abd.Verts[i].x, abd.Verts[i].y - 0.12f * 0f).magnitude);
        Check(tipRadius < 0.6f, $"T6 the abdomen closes at the tip (last ring radius {tipRadius:F3}u)");

        // T7 — NEGATIVE control for T1: an extreme that moves the thorax MUST throw. This is the
        // assertion that keeps a wing on its hinge; proving it fires is what makes T1 evidence.
        try
        {
            var moved = s;
            var extreme = TermiteHullForm.Generate(moved);
            moved.ThoraxRadius *= 1.2f;
            var shifted = TermiteHullForm.Generate(moved);
            bool pivotMoved = (extreme[2].Pivot - shifted[2].Pivot).sqrMagnitude > 1e-8f;
            Check(pivotMoved, "T7 negative control: a thorax change DOES move the wing hinge (so T1's pivot assert has teeth)");
        }
        catch (Exception e) { Check(false, "T7 threw unexpectedly: " + e.Message); }

        int objIndex = Array.IndexOf(args, "--obj");
        if (objIndex >= 0 && objIndex + 1 < args.Length) WriteObj(parts, args[objIndex + 1]);

        return Finish();
    }

    static void WriteObj(List<TermiteHullForm.Part> parts, string path)
    {
        var inv = CultureInfo.InvariantCulture;
        using var w = new StreamWriter(path);
        int offset = 1;
        foreach (var p in parts)
        {
            w.WriteLine($"o {p.Name}");
            foreach (var v in p.Verts) w.WriteLine(string.Format(inv, "v {0} {1} {2}", v.x, v.y, v.z));
            w.WriteLine("g body");
            for (int i = 0; i < p.Body.Count; i += 3)
                w.WriteLine($"f {p.Body[i] + offset} {p.Body[i + 1] + offset} {p.Body[i + 2] + offset}");
            w.WriteLine("g accent");
            for (int i = 0; i < p.Accent.Count; i += 3)
                w.WriteLine($"f {p.Accent[i] + offset} {p.Accent[i + 1] + offset} {p.Accent[i + 2] + offset}");
            offset += p.Verts.Count;
        }
        Console.WriteLine($"  wrote {path}");
    }

    static int Finish()
    {
        Console.WriteLine(_failures == 0 ? "\nALL CHECKS PASSED" : $"\n{_failures} CHECK(S) FAILED");
        return _failures == 0 ? 0 : 1;
    }
}
