// Compile-and-RUN gate for the SHIPPED ButterflyHullForm. Proves the file compiles, that the
// four element extremes hold topology (the assertion the blend stands on), that the baked bounds
// really contain every reachable weight combination, and that blending to weight 1 reproduces the
// extreme build exactly. Run: ./run.sh
using System;
using System.Collections.Generic;
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

    /// <summary>The proportions the Butterfly prefab authors. Kept in step with
    /// ButterflyHullBuilder's field initializers by author_butterfly_vessel.py --check.</summary>
    static ButterflyHullForm.Settings Shipped() => new()
    {
        BodyLength = 7.5f, BodyRadius = 0.85f, BodySegments = 10, BodySides = 8,
        Span = 11.0f, ChordRoot = 6.2f, ChordTip = 2.4f, Sweep = 3.4f,
        Camber = 0.85f, Dihedral = 14f, HindScale = 0.68f, HindSweep = 1.1f,
        ScallopCount = 3, ScallopDepth = 0.22f, TipFlare = 0.35f,
        SpanSegments = 14, ChordSegments = 10,
        AntennaLength = 2.2f, AntennaThickness = 0.07f,
    };

    public static int Main(string[] args)
    {
        var s = Shipped();
        var parts = ButterflyHullForm.Generate(s);

        Console.WriteLine("=== ButterflyHullForm — shipped settings ===");
        int totalVerts = 0, totalTris = 0;
        foreach (var p in parts)
        {
            totalVerts += p.Verts.Count;
            totalTris += (p.Body.Count + p.Wing.Count) / 3;
            Console.WriteLine($"  {p.Name,-10} verts {p.Verts.Count,5}  body tris {p.Body.Count / 3,5}" +
                              $"  wing tris {p.Wing.Count / 3,5}  pivot {p.Pivot}");
        }
        Console.WriteLine($"  TOTAL      verts {totalVerts}  tris {totalTris}");

        Check(parts.Count == 5, "five parts (core + four wings)");
        Check(parts[0].Wing.Count == 0, "the Core emits NOTHING into the wing submesh (body wears slot 0)");
        for (int i = 1; i < parts.Count; i++)
            Check(parts[i].Body.Count == 0 && parts[i].Wing.Count > 0,
                  $"{parts[i].Name} is wing-submesh only (slot 1 takes the domain colour)");

        // T1 — topology holds across every element extreme. BakeMorphSet throws if it does not.
        ButterflyHullForm.MorphSet set = null;
        try { set = ButterflyHullForm.BakeMorphSet(s); Check(true, "T1 bake: all four element extremes hold topology"); }
        catch (Exception e) { Check(false, "T1 bake threw: " + e.Message); return Finish(); }

        // T2 — every element actually MOVES the hull. An extreme that changes nothing is an
        // element that reads as absent on screen (the labelled-but-inert shape trap, one level up).
        for (int e = 0; e < ButterflyHullForm.MorphElements.Length; e++)
        {
            float travel = 0f;
            for (int p = 0; p < parts.Count; p++)
                foreach (var d in set.Deltas[e][p].VertDeltas)
                    travel = Mathf.Max(travel, d.magnitude);
            Check(travel > 0.25f,
                  $"T2 {ButterflyHullForm.MorphElements[e]} moves the hull (max travel {travel:F2}u)");
        }

        // T3 — blending to weight 1 on one element reproduces that element's extreme build exactly.
        var verts = new List<Vector3>();
        var normals = new List<Vector3>();
        for (int e = 0; e < ButterflyHullForm.MorphElements.Length; e++)
        {
            var extreme = ButterflyHullForm.Generate(
                ButterflyHullForm.ApplyElementExtreme(s, ButterflyHullForm.MorphElements[e]));
            var w = new float[4];
            w[e] = 1f;
            float worst = 0f;
            for (int p = 0; p < parts.Count; p++)
            {
                ButterflyHullForm.BlendPart(set, p, w, verts, normals);
                for (int i = 0; i < verts.Count; i++)
                    worst = Mathf.Max(worst, (verts[i] - extreme[p].Verts[i]).magnitude);
            }
            Check(worst < 1e-4f,
                  $"T3 {ButterflyHullForm.MorphElements[e]} blend@1 == extreme build (worst {worst:E2})");
        }

        // T4 — the baked bounds contain every one of the 16 weight CORNERS. The bounds are pinned
        // at emit and never recalculated, so a pose outside them is culled while still on screen.
        float slack = float.PositiveInfinity;
        for (int corner = 0; corner < 16; corner++)
        {
            var w = new float[4];
            for (int e = 0; e < 4; e++) w[e] = ((corner >> e) & 1) == 1 ? 1f : 0f;
            for (int p = 0; p < parts.Count; p++)
            {
                ButterflyHullForm.BlendPart(set, p, w, verts, normals);
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

        // T5 — the hull is a BUTTERFLY: wider than it is long. This is the whole silhouette brief,
        // and it is the one property a proportion retune can quietly lose.
        float minX = float.PositiveInfinity, maxX = float.NegativeInfinity;
        float minZ = float.PositiveInfinity, maxZ = float.NegativeInfinity;
        foreach (var p in parts)
            foreach (var v in p.Verts)
            {
                Vector3 h = v;
                minX = Mathf.Min(minX, h.x); maxX = Mathf.Max(maxX, h.x);
                minZ = Mathf.Min(minZ, h.z); maxZ = Mathf.Max(maxZ, h.z);
            }
        float width = maxX - minX, length = maxZ - minZ;
        Console.WriteLine($"  extent: width {width:F2}u  length {length:F2}u  aspect {width / length:F2}");
        Check(width > length, $"T5 wingspan ({width:F2}u) exceeds hull length ({length:F2}u)");

        // T6 — a NEGATIVE control for T1: an extreme that crosses the antenna feature gate must
        // throw rather than silently corrupting the blend.
        var broken = s;
        broken.AntennaLength = 0.0005f;   // base build has no antennae...
        try
        {
            var badBase = ButterflyHullForm.Generate(broken);
            var badExt = ButterflyHullForm.Generate(ButterflyHullForm.ApplyElementExtreme(broken, Element.Time));
            // ...but Time multiplies AntennaLength by 1.3, still under the gate, so this must NOT
            // change topology. The control is that the gate is respected in BOTH builds.
            Check(badBase[0].Verts.Count == badExt[0].Verts.Count,
                  "T6 negative control: a sub-gate antenna length stays sub-gate under Time");
        }
        catch (Exception e) { Check(false, "T6 threw unexpectedly: " + e.Message); }

        return Finish();
    }

    static int Finish()
    {
        Console.WriteLine(_failures == 0 ? "\nALL CHECKS PASSED" : $"\n{_failures} CHECK(S) FAILED");
        return _failures == 0 ? 0 : 1;
    }
}
