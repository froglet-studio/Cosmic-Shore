// Checks the Elemental Converter's round-5 pieces on the SHIPPED combine data (OmniCombineData.json, Unity frame):
//  1. OmniCombineMotion fits all 60 square plates to the data's three keys (pentagon, Step1, Step2) as rigid turns;
//  2. at full swell every square, rhombus and pentagon is the game's 1.1x thickness; the triangle plates do not move;
//  3. a finished pentagon / triangle turn lands the thickened squares CONGRUENT with rest (the snap back is invisible),
//     and every square stays rigid mid-turn;
//  4. OmniCombineCore's game-motion OmniPose: with no wave cued nothing moves (no squares weave, no flip); a cued wave
//     run to its end lands every rhombus congruent with rest, so the converter's reset between waves is invisible;
//  5. CrystalFrame reproduces Pose for every unmorphed crystal vertex, composed as the player composes it in Unity
//     (AngleAxis(tumble) * AngleAxis(spin), then the centre) - with the idle spin ON, so the order is exercised;
//  6. the Charge light reads both combine crystals (12 blooms; 12 pentagons) and the flux arcs' ends sit on the
//     pentagons as OmniCombineMotion swells them;
//  7. the lab path is untouched: GameMotion off gives the lab's OmniPose (the parity harness holds the lab itself).
// Negative controls: the square keys out of order are refused; composing the frame the other way round fails check 5.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using CosmicShore.Utility;
using UnityEngine;

static class Program
{
    static int failures;
    static void Check(string what, bool ok, string detail = "") { Console.WriteLine((ok ? "PASS  " : "FAIL  ") + what + (detail.Length > 0 ? "  (" + detail + ")" : "")); if (!ok) failures++; }
    static Vector3 V(float[] a, int i) => new(a[3 * i], a[3 * i + 1], a[3 * i + 2]);
    static Vector3 V(D3 d) => new((float)d.X, (float)d.Y, (float)d.Z);

    static int Main(string[] args)
    {
        var data = JsonSerializer.Deserialize<OmniCombineData>(File.ReadAllText(args[0]), new JsonSerializerOptions { IncludeFields = true });
        var core = new OmniCombineCore(data);
        float R = (float)core.OmniRadius;
        var ov = data.omni.verts;
        int n = ov.Length / 3;

        // 1
        bool fit = OmniCombineMotion.TryFit(data, R, 1.1f, out var motion, out var problem);
        Check("all 60 squares fit the data's keys as rigid turns", fit && motion.Squares.Length == 60 && motion.SquareCorners.All(c => c.Length == 8),
              fit ? $"{motion.Squares.Length} squares" : problem);
        if (!fit) return Finish();

        // 2
        var swelled = new float[ov.Length];
        motion.Swell(ov, 1f, swelled);
        var plates = Enumerable.Range(0, n).GroupBy(i => data.omni.plate[i]).ToList();
        float worstRatio = 0f, triMoved = 0f;
        foreach (var g in plates)
        {
            var idx = g.ToList();
            if (data.omni.plateOwner[g.Key] == OmniCombineCore.Mass) { foreach (int i in idx) triMoved = Math.Max(triMoved, (V(swelled, i) - V(ov, i)).magnitude); continue; }
            float Thick(float[] p)
            {
                var pts = idx.Select(i => V(p, i)).ToList();
                float lo = pts.Min(x => x.magnitude), hi = pts.Max(x => x.magnitude), mid = (lo + hi) / 2;
                var o = pts.Where(x => x.magnitude > mid).ToList(); var inn = pts.Where(x => x.magnitude <= mid).ToList();
                var oc = o.Aggregate(Vector3.zero, (a, b) => a + b) / o.Count; var ic = inn.Aggregate(Vector3.zero, (a, b) => a + b) / inn.Count;
                return (oc - ic).magnitude;
            }
            worstRatio = Math.Max(worstRatio, Math.Abs(Thick(swelled) / Thick(ov) - 1.1f));
        }
        Check("at full swell squares, rhombi and pentagons are 1.1x thick", worstRatio < 1e-4f, $"worst ratio error {worstRatio:E2}");
        Check("the triangle plates do not swell", triMoved == 0f, $"{triMoved:E2}");

        // 3
        var pose = new float[ov.Length];
        float worstLand = 0f, worstRigid = 0f;
        foreach (bool pentagon in new[] { true, false })
        {
            Array.Copy(swelled, pose, pose.Length);
            motion.TurnSquares(swelled, pose, pentagon, 1f, 0.14f * R, 1.5f, 1.5f);
            var restCorners = motion.SquareCorners.SelectMany(c => c).Select(i => V(swelled, i)).ToList();
            foreach (var c in motion.SquareCorners) foreach (int i in c) worstLand = Math.Max(worstLand, restCorners.Min(r => (r - V(pose, i)).magnitude));
            Array.Copy(swelled, pose, pose.Length);
            motion.TurnSquares(swelled, pose, pentagon, 0.5f, 0.14f * R, 1.5f, 1.5f);
            foreach (var c in motion.SquareCorners)
                for (int a = 0; a < c.Length; a++) for (int b = a + 1; b < c.Length; b++)
                    worstRigid = Math.Max(worstRigid, Math.Abs((V(pose, c[a]) - V(pose, c[b])).magnitude - (V(swelled, c[a]) - V(swelled, c[b])).magnitude));
        }
        Check("a finished turn lands the thickened squares congruent with rest", worstLand < 1e-4f * R, $"worst {worstLand / R:E2} R");
        Check("every square stays rigid mid-turn", worstRigid < 1e-4f * R, $"worst {worstRigid / R:E2} R");

        // 4
        var p = OmniCombineParams.LabDefault;
        p.GameMotion = true;
        p.ShaderDepth = true;   // as the converter plays it: the depth shader, not the CPU, raises the triangles
        var cam = new D3(0.3, 0.4, 1).Unit;
        var outPose = new float[ov.Length];
        core.OmniPose(50, p, 1, cam, outPose, swelled, -1, -1);
        float still = 0f;
        for (int i = 0; i < ov.Length; i++) still = Math.Max(still, Math.Abs(outPose[i] - swelled[i]));
        Check("game motion with no wave cued: nothing moves (no weave, no flip)", still == 0f, $"{still:E2}");
        float active = data.flip.loop - data.flip.first;
        float worstFlip = 0f, moved = 0f;
        for (long wave = 0; wave < 12; wave++)
        {
            core.OmniPose(50, p, 1, cam, outPose, swelled, data.flip.first + active, wave);
            var mid = new float[ov.Length];
            core.OmniPose(50, p, 1, cam, mid, swelled, data.flip.first + 0.6, wave);
            foreach (var g in plates.Where(g => data.omni.plateOwner[g.Key] == OmniCombineCore.Time))
            {
                var restSet = g.Select(i => V(swelled, i)).ToList();
                foreach (int i in g)
                {
                    worstFlip = Math.Max(worstFlip, restSet.Min(r => (r - V(outPose, i)).magnitude));
                    moved = Math.Max(moved, (V(mid, i) - V(swelled, i)).magnitude);
                }
            }
        }
        Check("a cued wave run to its end lands every rhombus congruent with rest", worstFlip < 1e-4f * R && moved > 0.05f * R,
              $"worst {worstFlip / R:E2} R; mid-wave a rhombus moves {moved / R:F2} R");

        // 5
        var q = OmniCombineParams.LabDefault;   // IdleTumble ON: the spin is non-zero while Charge waits
        q.GameMotion = true;
        var positions = Enumerable.Range(0, 4).Select(k => new float[3 * core.VertexCount(k)]).ToArray();
        var effect = new float[4];
        float worstFrame = 0f, worstSwapped = 0f;
        foreach (double t in new[] { 0.5, 1.7, OmniCombineCore.Departs(OmniCombineCore.Charge, q) + 0.2 })
        {
            core.Pose(t, q, cam, positions, effect);
            for (int k = 0; k < 4; k++)
            {
                if (k == OmniCombineCore.Mass) continue;   // Mass's idle pose has the depth seat folded in on the CPU in the lab path
                if (k == OmniCombineCore.Time || k == OmniCombineCore.Space) continue;   // their idle effects move vertices
                core.CrystalFrame(k, t, q, out var c, out var spinAxis, out double spin, out var tumbleAxis, out double tumble);
                var rot = Quaternion.AngleAxis((float)(tumble * Mathf.Rad2Deg), V(tumbleAxis)) * Quaternion.AngleAxis((float)(spin * Mathf.Rad2Deg), V(spinAxis));
                var swapped = Quaternion.AngleAxis((float)(spin * Mathf.Rad2Deg), V(spinAxis)) * Quaternion.AngleAxis((float)(tumble * Mathf.Rad2Deg), V(tumbleAxis));
                var basis = core.CrystalData(k).basis;
                for (int i = 0; i < basis.Length / 3; i++)
                {
                    var want = V(positions[k], i);
                    worstFrame = Math.Max(worstFrame, (rot * V(basis, i) + V(c) - want).magnitude);
                    worstSwapped = Math.Max(worstSwapped, (swapped * V(basis, i) + V(c) - want).magnitude);
                }
            }
        }
        Check("CrystalFrame, composed as the player composes it, reproduces Pose for the unmorphed Charge crystal", worstFrame < 1e-4f * R,
              $"worst {worstFrame / R:E2} R");
        Check("negative control: the frame composed the other way round does not", worstSwapped > 1e-2f * R, $"worst {worstSwapped / R:E2} R");

        // 6
        var charge = core.CrystalData(OmniCombineCore.Charge);
        bool blooms = ChargeSites.TryBuild(Enumerable.Range(0, charge.basis.Length / 3).Select(i => V(charge.basis, i)).ToArray(), charge.tris, 1f, out var cs, out problem);
        Check("the Charge light reads the combine's Charge crystal as 12 blooms with the dodecahedral seam net",
              blooms && cs.Kind == ChargeSiteKind.Blooms && cs.Seams.Length == 30, problem ?? "");
        bool pents = ChargeSites.TryBuild(Enumerable.Range(0, n).Select(i => V(ov, i)).ToArray(), data.omni.tris, 1.1f, out var os, out problem);
        Check("the Charge light reads the combine's omni as 12 pentagons", pents && os.Kind == ChargeSiteKind.Pentagons, problem ?? "");
        if (pents)
        {
            var sw = Enumerable.Range(0, n).Select(i => V(swelled, i)).ToList();
            float worstEnd = 0f;
            for (int s = 0; s < 12; s++)
                foreach (var c in os.Corners[s])
                {
                    var lifted = c - os.Axes[s] * (0.012f * os.Radius);
                    worstEnd = Math.Max(worstEnd, sw.Min(v => (v - lifted).magnitude));
                }
            Check("the flux arcs end on the pentagons as the converter swells them", worstEnd < 2e-3f * R, $"worst {worstEnd / R:E2} R (the lift is along the plate normal, not the axis)");
        }

        // 7
        var lab = OmniCombineParams.LabDefault;
        var a1 = new float[ov.Length]; var a2 = new float[ov.Length];
        core.OmniPose(13.3, lab, 1, cam, a1);
        core.OmniPose(13.3, lab, 1, cam, a2, swelled, 0.5, 3);
        Check("GameMotion off ignores the game-motion arguments (the lab path)", a1.SequenceEqual(a2));

        // negative control: keys out of order
        var swappedData = JsonSerializer.Deserialize<OmniCombineData>(File.ReadAllText(args[0]), new JsonSerializerOptions { IncludeFields = true });
        (swappedData.squares.keys[0], swappedData.squares.keys[1]) = (swappedData.squares.keys[1], swappedData.squares.keys[0]);
        Check("negative control: square keys out of order are refused", !OmniCombineMotion.TryFit(swappedData, R, 1.1f, out _, out var why), why ?? "accepted");
        return Finish();
    }

    static int Finish()
    {
        Console.WriteLine(failures == 0 ? "ALL PASS" : $"{failures} FAILED");
        return failures == 0 ? 0 : 1;
    }
}
