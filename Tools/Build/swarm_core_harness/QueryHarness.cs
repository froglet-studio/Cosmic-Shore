// Round 11a (Docs/SWARM_FAUNA.md §19.1): a swarm member is found through the PLATFORM's own virtual-entry
// queries now (PrismSpatialIndex.QuerySphereVirtualIds / QuerySegmentVirtualIds / QueryConeVirtualIds) - the round-8
// member grid and SwarmVolume are retired. This runs those queries' shapes against the shipped prism predicates:
//   R11a  every virtual-id query shape (VirtualQueryShape + the factory each query builds it with, EXTRACTED
//         verbatim from PrismSpatialIndex.cs by extract_burst_predicates.py) agrees with the shipped predicate the
//         same-named prism query runs (AOESpatialQueryJob, QuerySegment's DistanceToSegmentSq, QueryCone's
//         preprocessing + ConeContains) on random volumes and points - and each of three MUTANT shapes (a predicate
//         that is plausibly wrong) disagrees, so the test can fail. The AOE slabs need no test of their own: the
//         Burst jobs walk the index's _spatial array, virtual entries included, with their unchanged predicates.
// The member side (where the stored point is vs where the body is drawn, the count-once ledger, the pose and the
// entity ledger) is R11b-R11e in TickJobHarness, which has a live swarm to read. Compiled as its own executable by
// run.sh. Exit code is non-zero on any failure.
using System;
using System.Collections.Generic;
using System.Numerics;
using ShippedPrismQuery;
using Unity.Collections;
using F3 = Unity.Mathematics.float3;

static class QueryHarness
{
    static int _fail;
    static void Check(bool ok, string what)
    {
        Console.WriteLine($"  [{(ok ? "ok" : "FAIL")}] {what}");
        if (!ok) _fail++;
    }

    static Random _r = new(11);
    static float U(float a, float b) => a + (float)_r.NextDouble() * (b - a);
    static Vector3 V(float s) => new(U(-s, s), U(-s, s), U(-s, s));
    static Vector3 Dir() { Vector3 d; do d = V(1); while (d.LengthSquared() < 0.05f || d.LengthSquared() > 1); return d; }

    // ── the shipped answers, through the extracted jobs exactly as the index runs them ──
    static readonly List<AOEHit> Hits = new();
    static bool JobHas<TJob>(TJob job) where TJob : struct, Unity.Jobs.IJobParallelFor
    {
        Hits.Clear(); job.Execute(0); return Hits.Count > 0;
    }
    static NativeArray<PrismSpatialData> One(Vector3 p) =>
        new(new[] { new PrismSpatialData { Position = p, Flags = PrismFlags.IsActive } });
    static NativeList<AOEHit>.ParallelWriter W => new NativeList<AOEHit>(Hits).AsParallelWriter();

    public static int Main(string[] args)
    {
        const int Volumes = 400, Points = 2500;
        Console.WriteLine("R11a. the virtual-entry query shapes == the shipped prism predicates (both extracted verbatim from PrismSpatialIndex.cs)");
        string[] names = { "QuerySphereVirtualIds vs AOESpatialQueryJob", "QuerySegmentVirtualIds vs QuerySegment", "QueryConeVirtualIds vs QueryCone" };
        string[] mutants = { "sphere: strict < instead of <=", "capsule: unclamped segment parameter", "cone: minimum radius dropped" };
        for (int kind = 0; kind < 3; kind++)
        {
            long n = 0, bad = 0, mutBad = 0, inside = 0;
            for (int v = 0; v < Volumes; v++)
            {
                var a = V(300); var d = Dir();
                float r = U(5, 120), len = U(50, 3000), half = U(0.2f, 25f), minR = U(0f, 8f);
                if (kind == 2 && v % 10 == 0) half = 0f;   // the sniper's degenerate 'needle' + floor
                var b = a + d * len * 0.1f;

                Shipped.VirtualQueryShape shape, mutantShape = default;
                bool made = kind switch
                {
                    0 => Shipped.VSphere(a, r, out shape),
                    1 => Shipped.VSegment(a, b, r, out shape),
                    _ => Shipped.VCone(a, d, len, half, minR, out shape),
                };
                if (kind == 2) Shipped.VCone(a, d, len, half, 0f, out mutantShape);
                // shipped preprocessing
                Shipped.QueryConePrep(a, d, len, half, minR, out F3 cdir, out float cTan, out float cMin);
                F3 ab = (F3)b - (F3)a; float abLenSq = Unity.Mathematics.math.lengthsq(ab);

                Vector3 mid, ext;
                if (kind == 2) { mid = a + d / d.Length() * len * 0.5f; ext = new Vector3(len * 0.6f + MathF.Max(minR, len * MathF.Tan(half * MathF.PI / 180f))); }
                else { mid = (a + b) * 0.5f; ext = new Vector3((b - a).Length() * 0.6f + r * 1.2f); }
                for (int q = 0; q < Points; q++)
                {
                    var p = mid + new Vector3(U(-ext.X, ext.X), U(-ext.Y, ext.Y), U(-ext.Z, ext.Z));
                    bool shipped = kind switch
                    {
                        0 => JobHas(new AOESpatialQueryJob { Prisms = One(p), Center = a, RadiusSq = r * r, BlastOrigin = a, Hits = W }),
                        1 => Shipped.DistanceToSegmentSq(p, a, ab, abLenSq) <= r * r,
                        _ => Shipped.ConeContains(p, a, cdir, len, cTan, cMin),
                    };
                    bool ours = made && shape.Contains(p);
                    bool mutant = kind switch
                    {
                        0 => Vector3.DistanceSquared(p, a) < r * r * 0.98f,
                        1 => Vector3.DistanceSquared(p, a + (Vector3)ab * (abLenSq > 1e-8f ? Vector3.Dot(p - a, ab) / abLenSq : 0f)) <= r * r,
                        _ => mutantShape.Contains(p),
                    };
                    n++; if (shipped) inside++;
                    if (ours != shipped) bad++;
                    if (mutant != shipped) mutBad++;
                }
            }
            double rate = n > 0 ? (double)bad / n : 1, mrate = n > 0 ? (double)mutBad / n : 0;
            Console.WriteLine($"  {names[kind],-46} {n,9} points, {inside * 100.0 / Math.Max(1, n),5:F1}% inside, disagreements {bad} ({rate:E2})");
            Check(bad == 0 && inside > 0, $"{names[kind]}: identical to the shipped predicate");
            Console.WriteLine($"     mutant '{mutants[kind]}': {mutBad} disagreements ({mrate:E2})");
            Check(mutBad > 0, $"negative control fires: '{mutants[kind]}' is caught");
        }

        Console.WriteLine(_fail == 0 ? "\nquery: OK" : $"\nquery: {_fail} FAILED");
        return _fail == 0 ? 0 : 1;
    }
}
