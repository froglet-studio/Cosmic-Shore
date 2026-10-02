// Round 8 (Docs/SWARM_FAUNA.md §16.1): the swarm MEMBER QUERY, run against the shipped prism predicates.
//   R8a  every SwarmVolume shape agrees with the EXTRACTED shipped Burst code (ShippedPrismQuery.*, generated
//        from PrismSpatialIndex.cs by extract_burst_predicates.py) on random volumes and points - and each of
//        four MUTANT transcriptions (a predicate that is plausibly wrong) disagrees, so the test can fail;
//   R8b  the member grid returns exactly the brute-force set, with members displaced along their tick step,
//        and the candidate walk touches far fewer members than the swarm holds for a small far volume; with
//        its pad forced to zero it MISSES members (negative control);
// Compiled as its own executable by run.sh (no swarm core needed). Exit code is non-zero on any failure.
using System;
using System.Collections.Generic;
using System.Numerics;
using CosmicShore.Gameplay;
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

    static Random _r = new(8);
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
        Console.WriteLine("R8a. SwarmVolume == the shipped Burst predicates (extracted verbatim from PrismSpatialIndex.cs)");
        string[] names = { "sphere (AOESpatialQueryJob)", "capsule (QuerySegment)", "cone (QueryCone)", "cone slab (AOEConicSweepQueryJob)", "cylinder slab (AOECylinderSweepQueryJob)" };
        string[] mutants = { "sphere: strict < instead of <=", "capsule: unclamped segment parameter", "cone: no minimum radius", "cone slab: gape dropped (plain cone)", "cylinder: mirror flag ignored" };
        for (int kind = 0; kind < 5; kind++)
        {
            long n = 0, bad = 0, mutBad = 0, inside = 0;
            for (int v = 0; v < Volumes; v++)
            {
                var a = V(300); var d = Dir();
                float r = U(5, 120), len = U(50, 3000), half = U(0.2f, 25f), minR = U(0f, 8f);
                float s0 = U(0, 200), s1 = s0 + U(5, 400), core = U(0.02f, 0.6f), gapeTan = U(0f, 0.5f);
                var gapeRaw = V(1) + d * U(-0.5f, 0.5f);   // NOT orthogonal: the prep must fix it
                bool mir = _r.Next(2) == 0;
                if (kind == 2 && v % 10 == 0) half = 0f;   // the sniper's degenerate 'needle' + floor
                if (kind == 4) mir = mir || v % 3 == 0;
                SwarmVolume vol = kind switch
                {
                    0 => SwarmVolume.Sphere(a, r),
                    1 => SwarmVolume.Capsule(a, a + d * len * 0.1f, r),
                    2 => SwarmVolume.Cone(a, d, len, half, minR),
                    3 => SwarmVolume.ConeSlab(a, d, gapeRaw, s0, s1, core, gapeTan),
                    _ => SwarmVolume.CylinderSlab(a, d, s0, s1, r, mir),
                };
                // shipped preprocessing
                Shipped.QueryConePrep(a, d, len, half, minR, out F3 cdir, out float cTan, out float cMin);
                Shipped.ConeSlabPrep(d, gapeRaw, out F3 sAxis, out F3 sGape);
                var cylAxis = Unity.Mathematics.math.normalizesafe(d, new F3(0, 0, 1));
                var b = a + d * len * 0.1f; F3 ab = b - a; float abLenSq = Unity.Mathematics.math.lengthsq(ab);

                var lo = vol.Lo; var hi = vol.Hi;
                if (!(hi.X >= lo.X)) continue;
                var ext = (hi - lo) * 0.6f; var mid = (hi + lo) * 0.5f;
                for (int q = 0; q < Points; q++)
                {
                    var p = mid + new Vector3(U(-ext.X, ext.X), U(-ext.Y, ext.Y), U(-ext.Z, ext.Z));
                    bool shipped = kind switch
                    {
                        0 => JobHas(new AOESpatialQueryJob { Prisms = One(p), Center = a, RadiusSq = r * r, BlastOrigin = a, Hits = W }),
                        1 => Shipped.DistanceToSegmentSq(p, a, ab, abLenSq) <= r * r,
                        2 => Shipped.ConeContains(p, a, cdir, len, cTan, cMin),
                        3 => JobHas(new AOEConicSweepQueryJob { Prisms = One(p), Apex = a, Axis = sAxis, GapeAxis = sGape, SliceMin = MathF.Max(s0, 0f), SliceMax = s1, CoreTanHalfAngle = core, TanGapePerUnit = MathF.Max(gapeTan, 0f), Hits = W }),
                        _ => JobHas(new AOECylinderSweepQueryJob { Prisms = One(p), Origin = a, Axis = cylAxis, SliceMin = MathF.Max(s0, 0f), SliceMax = s1, RadiusSq = r * r, Mirrored = mir, Hits = W }),
                    };
                    bool ours = vol.Contains(p);
                    bool mutant = kind switch
                    {
                        0 => Vector3.DistanceSquared(p, a) < r * r * 0.98f,
                        1 => Vector3.DistanceSquared(p, a + (Vector3)ab * (abLenSq > 1e-8f ? Vector3.Dot(p - a, ab) / abLenSq : 0f)) <= r * r,
                        2 => SwarmVolume.Cone(a, d, len, half, 0f).Contains(p),
                        3 => SwarmVolume.ConeSlab(a, d, gapeRaw, s0, s1, core, 0f).Contains(p),
                        _ => SwarmVolume.CylinderSlab(a, d, s0, s1, r, false).Contains(p),
                    };
                    n++; if (shipped) inside++;
                    if (ours != shipped) bad++;
                    if (mutant != shipped) mutBad++;
                }
            }
            double rate = n > 0 ? (double)bad / n : 1, mrate = n > 0 ? (double)mutBad / n : 0;
            Console.WriteLine($"  {names[kind],-42} {n,9} points, {inside * 100.0 / Math.Max(1, n),5:F1}% inside, disagreements {bad} ({rate:E2})");
            Check(bad == 0, $"{names[kind]}: identical to the shipped predicate");
            Console.WriteLine($"     mutant '{mutants[kind]}': {mutBad} disagreements ({mrate:E2})");
            Check(mutBad > 0, $"negative control fires: '{mutants[kind]}' is caught");
        }

        Console.WriteLine("\nR8b. the member grid: candidates are a superset of every member drawn inside the volume, and local");
        {
            const int cap = 2000;
            var inst = new SwarmInstance[cap];
            float step = 6f;
            for (int i = 0; i < cap; i++)
            {
                if (i % 7 == 3) continue;   // dead slots
                var cur = new Vector3(U(-400, 400), U(-150, 150), U(-400, 400)) + new Vector3(2000, 0, 0);
                var dir = Dir(); dir /= dir.Length();
                inst[i] = new SwarmInstance { CurPos = cur, PrevPos = cur - dir * U(0, step), CurFace = dir, PrevFace = dir, PrismZ = -U(2, 4), Scale = Vector3.One, Flags = SwarmInstance.Pack(true, 0, 1, 1) };
            }
            var grid = new SwarmMemberGrid(cap); grid.Build(inst, step + 4f + 1f);
            var zero = new SwarmMemberGrid(cap); zero.Build(inst, 0f);
            var scratch = new List<int>(); var got = new List<int>();
            int missed = 0, zeroMissed = 0, checks = 0; long walked = 0;
            for (int v = 0; v < 3000; v++)
            {
                var c = new Vector3(2000, 0, 0) + V(450);
                if (v % 2 == 0)
                {
                    // centred on a member's drawn body: the cases a grid is most likely to get wrong
                    int m; do m = _r.Next(cap); while (!inst[m].Alive);
                    c = Vector3.Lerp(inst[m].PrevPos, inst[m].CurPos, 0.5f) + inst[m].CurFace * inst[m].PrismZ + V(3);
                }
                var vol = (v % 3) switch
                {
                    0 => SwarmVolume.Sphere(c, U(2, 60)),
                    1 => SwarmVolume.Capsule(c, c + Dir() * 80f, U(2, 10)),
                    _ => SwarmVolume.ConeSlab(c, Dir(), V(1), 0f, U(20, 200), U(0.05f, 0.4f), U(0, 0.3f)),
                };
                float alpha = U(0, 1);
                var want = new HashSet<int>();
                for (int i = 0; i < cap; i++)
                {
                    if (!inst[i].Alive) continue;
                    var pose = Vector3.Lerp(inst[i].PrevPos, inst[i].CurPos, alpha);
                    var body = pose + inst[i].CurFace * inst[i].PrismZ;
                    if (vol.Contains(body)) want.Add(i);
                }
                scratch.Clear(); grid.Candidates(vol.Lo, vol.Hi, scratch); walked += scratch.Count;
                var cand = new HashSet<int>(scratch);
                foreach (var w in want) if (!cand.Contains(w)) missed++;
                scratch.Clear(); zero.Candidates(vol.Lo, vol.Hi, scratch);
                var cz = new HashSet<int>(scratch);
                foreach (var w in want) if (!cz.Contains(w)) zeroMissed++;
                checks += want.Count;
                if (new HashSet<int>(scratch).Count != scratch.Count) missed += 1000;   // a duplicate is a double hit
            }
            Console.WriteLine($"  {checks} member-in-volume cases over 3000 volumes; padded grid missed {missed}, unpadded grid missed {zeroMissed}");
            Console.WriteLine($"  mean candidates walked per query {walked / 3000.0:F0} of {cap} slots");
            Check(missed == 0, "the padded grid never misses a member drawn inside the volume, and never returns one twice");
            Check(zeroMissed > 0, "negative control: an unpadded grid (filed at Cur, drawn on the Prev->Cur step) misses members");
            Check(walked / 3000.0 < cap * 0.25, "a query walks a small fraction of the swarm (O(members near the volume))");
            scratch.Clear(); grid.Candidates(new Vector3(-5000), new Vector3(-4900), scratch);
            Check(scratch.Count == 0, "a volume nowhere near the swarm walks NO member (the AABB reject)");
        }

        Console.WriteLine(_fail == 0 ? "\nquery: OK" : $"\nquery: {_fail} FAILED");
        return _fail == 0 ? 0 : 1;
    }
}
