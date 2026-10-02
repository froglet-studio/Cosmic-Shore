// Runs the SHIPPED FoldGateGeometry against a simulated pilot, then asserts the properties the
// Butterfly's gates promise. Every negative control must fire, or the check it guards is proving
// nothing.
using System;
using System.Collections.Generic;
using UnityEngine;
using CosmicShore.Gameplay;

static class Program
{
    // The shipped ButterflyFoldAction.asset.
    const float R = 55f;         // gateRadius
    const float CLEAR = 40f;     // gateExitClearance
    const float STEP = 55f / 30f * 20f;   // ~37 u, a Butterfly at cruise stepping 20 physics frames

    static readonly Vector3 AXIS = new Vector3(0, 0, 1);
    static readonly Vector3 A = new Vector3(0, 0, 0);          // origin gate
    static readonly Vector3 B = new Vector3(0, 0, 900);        // destination gate, one fold away

    static int failures;

    static void Check(string name, bool ok, string detail = "")
    {
        Console.WriteLine($"  [{(ok ? "PASS" : "FAIL")}] {name}{(detail.Length > 0 ? "  " + detail : "")}");
        if (!ok) failures++;
    }

    /// <summary>
    /// One pilot, one pair of gates, flown as a sequence of positions. Mirrors FoldGate.Update's
    /// per-vessel bookkeeping EXACTLY -- arm latch, two-sample rule, transit, re-seed and disarm --
    /// and calls the shipped geometry for every decision.
    /// </summary>
    sealed class Sim
    {
        public Vector3 NearC, FarC, NearAx, FarAx;
        readonly Dictionary<string, Vector3> _last = new();
        readonly HashSet<string> _armed = new();
        public int Transits;

        public Sim(Vector3 a, Vector3 b, Vector3 axis) { NearC = a; FarC = b; NearAx = FarAx = axis; }

        // Returns the pilot's position after this sample (a transit moves them).
        public Vector3 Sample(Vector3 cur)
        {
            foreach (var gate in new[] { 0, 1 })
            {
                Vector3 c = gate == 0 ? NearC : FarC;
                Vector3 ax = gate == 0 ? NearAx : FarAx;
                Vector3 oc = gate == 0 ? FarC : NearC;
                Vector3 oax = gate == 0 ? FarAx : NearAx;
                string key = "g" + gate;

                bool first = !_last.TryGetValue(key, out var prev);
                _last[key] = cur;

                if (!_armed.Contains(key))
                {
                    if (!FoldGateGeometry.InNearZone(cur, c, ax, R, CLEAR)) _armed.Add(key);
                    continue;
                }
                if (first) continue;
                if (!FoldGateGeometry.CrossedMouth(prev, cur, c, ax, R, out var hit)) continue;

                Vector3 exit = FoldGateGeometry.Through(cur, c, ax, oc, oax);
                Transits++;
                _last["g0"] = exit; _last["g1"] = exit;
                _armed.Remove("g0"); _armed.Remove("g1");
                return exit;
            }
            return cur;
        }
    }

    static void Main()
    {
        Console.WriteLine("fold gate geometry -- running the shipped FoldGateGeometry.cs\n");

        // 1. THE DEFECT THIS RULE EXISTS FOR. The destination gate is laid AROUND the arriving
        //    pilot, so flying straight out of it must not send them home.
        {
            var sim = new Sim(A, B, AXIS);
            Vector3 p = B;                                  // standing in the far gate's mouth
            for (int i = 0; i < 40; i++) p = sim.Sample(p + AXIS * STEP);
            Check("a pilot flying out of their own arrival gate is not taken",
                  sim.Transits == 0, $"transits={sim.Transits}");
        }
        // 1n. NEGATIVE CONTROL: drop the arm latch and the same flight teleports.
        {
            Vector3 prev = B, cur = B + AXIS * STEP;
            bool crossed = FoldGateGeometry.CrossedMouth(prev, cur, B, AXIS, R, out _);
            Check("  (control) without the latch that first step DOES cross", crossed);
        }

        // 2. Once clear, flying back in is taken.
        {
            var sim = new Sim(A, B, AXIS);
            Vector3 p = B;
            for (int i = 0; i < 20; i++) p = sim.Sample(p + AXIS * STEP);   // fly clear
            int before = sim.Transits;
            for (int i = 0; i < 40 && sim.Transits == before; i++) p = sim.Sample(p - AXIS * STEP);
            Check("having flown clear, flying back through IS taken", sim.Transits == before + 1,
                  $"transits={sim.Transits}");
        }

        // 3. A transit deposits the pilot INSIDE the far gate's near zone for any step shorter
        //    than the zone's depth -- which is what makes disarming at the far end sufficient.
        {
            bool allInside = true;
            float depth = FoldGateGeometry.NearZoneDepth(R, CLEAR);
            var rng = new Random(7);
            for (int i = 0; i < 4000; i++)
            {
                // a crossing anywhere in the mouth, either way, with a step up to the zone depth
                double th = rng.NextDouble() * Math.PI * 2, rr = Math.Sqrt(rng.NextDouble()) * R * 0.95;
                float sense = rng.NextDouble() < 0.5 ? 1f : -1f;
                float past = (float)rng.NextDouble() * depth * 0.99f;
                Vector3 cur = A + new Vector3((float)(Math.Cos(th) * rr), (float)(Math.Sin(th) * rr), 0)
                                + AXIS * (sense * past);
                Vector3 exit = FoldGateGeometry.Through(cur, A, AXIS, B, AXIS);
                if (!FoldGateGeometry.InNearZone(exit, B, AXIS, R, CLEAR)) allInside = false;
            }
            Check("every transit of a step shorter than the zone depth lands inside it", allInside);
        }
        // 3n. NEGATIVE CONTROL: a step one unit past the zone depth lands outside it.
        {
            Vector3 cur = A + AXIS * (FoldGateGeometry.NearZoneDepth(R, CLEAR) + 1f);
            Vector3 exit = FoldGateGeometry.Through(cur, A, AXIS, B, AXIS);
            Check("  (control) a step past the zone depth lands outside",
                  !FoldGateGeometry.InNearZone(exit, B, AXIS, R, CLEAR));
        }

        // 4. SEAMLESS: the pilot's position relative to the mouth is carried through EXACTLY --
        //    lateral offset and axial depth alike -- so the jump is a change of frame with no
        //    lurch in it, and a camera/ribbon carried by the same map lands where it would have.
        {
            float worst = 0f;
            var rng = new Random(11);
            for (int i = 0; i < 4000; i++)
            {
                Vector3 rel = new Vector3((float)(rng.NextDouble() * 2 - 1) * R,
                                          (float)(rng.NextDouble() * 2 - 1) * R,
                                          (float)(rng.NextDouble() * 2 - 1) * 60f);
                Vector3 exit = FoldGateGeometry.Through(A + rel, A, AXIS, B, AXIS);
                worst = Math.Max(worst, ((exit - B) - rel).magnitude);
            }
            Check("position relative to the mouth is carried through exactly", worst < 1e-3f,
                  $"worst={worst:E2}u");
        }
        // 4n. NEGATIVE CONTROL: the retired re-projection (hit re-laid on the far plane plus a
        //     fixed clearance) lurches the pilot along the axis on the frame of the jump.
        {
            Vector3 cur = A + new Vector3(10f, -4f, 1.2f);          // 1.2u past the near plane
            Vector3 hit = A + new Vector3(10f, -4f, 0f);
            Vector3 oldExit = B + (hit - A) + AXIS * CLEAR;         // what the old rule produced
            Vector3 seamless = FoldGateGeometry.Through(cur, A, AXIS, B, AXIS);
            float lurch = Vector3.Dot(oldExit - seamless, AXIS);
            Check("  (control) the retired rule lurched the pilot forward", lurch > 30f,
                  $"lurch={lurch:F1}u");
        }

        // 5. The side you were heading for is the side you come out on.
        {
            Vector3 fwd = FoldGateGeometry.Through(A + AXIS * 0.5f, A, AXIS, B, AXIS);
            Vector3 back = FoldGateGeometry.Through(A - AXIS * 0.5f, A, AXIS, B, AXIS);
            bool ok = Vector3.Dot(fwd - B, AXIS) > 0f && Vector3.Dot(back - B, AXIS) < 0f;
            Check("momentum reads through the gate (sense is preserved)", ok);
        }

        // 6. A flight that misses the mouth is never taken, however close it passes.
        {
            var sim = new Sim(A, B, AXIS);
            Vector3 p = A + new Vector3(R * 1.05f, 0, -600f);
            for (int i = 0; i < 60; i++) p = sim.Sample(p + AXIS * STEP);
            Check("a pass just outside the rim is never taken", sim.Transits == 0,
                  $"transits={sim.Transits}");
        }
        // 6n. NEGATIVE CONTROL: the same flight just INSIDE the rim is taken.
        {
            var sim = new Sim(A, B, AXIS);
            Vector3 p = A + new Vector3(R * 0.95f, 0, -600f);
            for (int i = 0; i < 60; i++) p = sim.Sample(p + AXIS * STEP);
            Check("  (control) the same flight just inside the rim IS taken", sim.Transits >= 1,
                  $"transits={sim.Transits}");
        }

        // 7. A round trip is an involution: through A and back through B, you are where you were.
        {
            Vector3 p = A + new Vector3(12f, -7f, 3f);
            Vector3 there = FoldGateGeometry.Through(p, A, AXIS, B, AXIS);
            Vector3 home = FoldGateGeometry.Through(there, B, AXIS, A, AXIS);
            Check("a round trip returns you exactly to where you set off",
                  Vector3.Distance(home, p) < 1e-3f, $"delta={Vector3.Distance(home, p):E2}u");
        }

        // 8. The mouth points: OnPlane lies on the plane at the pilot's lateral offset, and its
        //    image through the pair lies on the far plane -- where one ribbon ends and the next
        //    begins.
        {
            Vector3 cur = A + new Vector3(20f, 5f, 2.5f);
            Vector3 near = FoldGateGeometry.OnPlane(cur, A, AXIS);
            Vector3 far = FoldGateGeometry.Through(near, A, AXIS, B, AXIS);
            bool ok = Math.Abs(FoldGateGeometry.Axial(near, A, AXIS)) < 1e-4f
                   && Math.Abs(FoldGateGeometry.Axial(far, B, AXIS)) < 1e-4f
                   && Math.Abs(FoldGateGeometry.Lateral(far, B, AXIS)
                               - FoldGateGeometry.Lateral(cur, A, AXIS)) < 1e-4f;
            Check("ribbon cut points lie on both planes at the pilot's offset", ok);
        }

        Console.WriteLine(failures == 0 ? "\nall checks passed" : $"\n{failures} FAILED");
        Environment.Exit(failures == 0 ? 0 : 1);
    }
}
