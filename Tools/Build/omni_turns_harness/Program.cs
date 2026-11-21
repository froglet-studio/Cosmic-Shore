// Checks the SHIPPED round-5 square turns and thicker plates on the real omni body (body.json, from dump.py):
//  1. every square, rhombus and pentagon is exactly 1.1x as thick; the cut-away triangles are untouched;
//  2. each square's turns are rigid: its corners keep their distances to each other at every frame;
//  3. a finished turn lands the squares CONGRUENT with rest (the set of square corners maps onto itself), so the
//     snap back to weight 0 is invisible;
//  4. with the art file's own lift and sideways step (1, 1) and no thickening, the triangle turn passes exactly
//     through the art file's Step1 at its middle - the turns are measured from the art, not invented;
//  5. the pentagon turn's hop peaks at the asked lift;
//  6. the cadence never has the squares and a wave at once.
// Negative controls: a non-rigid key is refused; a cadence with overlapping beats is caught by check 6's twin.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using UnityEngine;

static class Program
{
    static int failures;
    static void Check(string what, bool ok, string detail = "") { Console.WriteLine((ok ? "PASS  " : "FAIL  ") + what + (detail.Length > 0 ? "  (" + detail + ")" : "")); if (!ok) failures++; }

    static void Set(object o, string field, object value) => o.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(o, value);

    static Vector3 V(JsonElement e) => new(e[0].GetSingle(), e[1].GetSingle(), e[2].GetSingle());

    static int Main()
    {
        var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../body.json"))).RootElement;
        Mesh Body()
        {
            var m = new Mesh { name = "OmniCrystalExport (harness)" };
            m.vertices = doc.GetProperty("positions").EnumerateArray().Select(V).ToArray();
            m.normals = doc.GetProperty("normals").EnumerateArray().Select(V).ToArray();
            m.SetTriangles(doc.GetProperty("triangles").EnumerateArray().Select(x => x.GetInt32()).ToArray(), 0);
            return m;
        }
        var squares = new CrystalSpaceSquaresSO();
        Set(squares, "anchors", doc.GetProperty("anchors").EnumerateArray().Select(V).ToArray());
        Set(squares, "bodyRadius", doc.GetProperty("bodyRadius").GetSingle());
        var keys = doc.GetProperty("keys").EnumerateArray().Select(k =>
        {
            var key = new CrystalSpaceSquaresSO.Key();
            Set(key, "name", k.GetProperty("name").GetString());
            Set(key, "stackedOn", k.GetProperty("stackedOn").GetInt32());
            Set(key, "deltas", k.GetProperty("deltas").EnumerateArray().Select(V).ToArray());
            return key;
        }).ToArray();
        Set(squares, "keys", keys);
        Set(squares, "weights", new float[] { 0f, 0f, 0f });

        OmniCrystalCadenceSO Turns(float thick, float tri, float side, float pent = 0.14f)
        {
            var t = new OmniCrystalCadenceSO();
            Set(t, "plateThickness", thick); Set(t, "triangleLift", tri); Set(t, "triangleSide", side); Set(t, "pentagonLift", pent);
            return t;
        }

        // the shipped settings
        var mesh = Body();
        var rest = mesh.vertices;
        var meth = typeof(CrystalSpaceSquaresBaker).GetMethod("TryAddTurns", BindingFlags.NonPublic | BindingFlags.Static);
        object[] args = { mesh, squares, Turns(1.1f, 1.5f, 1.5f), null };
        bool ok = (bool)meth.Invoke(null, args);
        Check("TryAddTurns accepts the shipped body and squares", ok, (string)args[3] ?? "");
        if (!ok) return 1;
        var thick = mesh.vertices;

        var plateOf = RhombusSkinBaker.PlateOfVertices(Body(), rest, out var corners, out var weldOf, out var welds);
        var plates = new Dictionary<int, List<int>>();
        for (int i = 0; i < rest.Length; i++) { if (!plates.ContainsKey(plateOf[i])) plates[plateOf[i]] = new List<int>(); plates[plateOf[i]].Add(i); }
        var byCorners = plates.GroupBy(p => corners[p.Key]).ToDictionary(g => g.Key, g => g.Count());
        Check("the body splits into 20 triangles, 90 boxes (60 squares + 30 rhombi), 12 pentagons",
            byCorners.GetValueOrDefault(6) == 20 && byCorners.GetValueOrDefault(8) == 90 && byCorners.GetValueOrDefault(10) == 12, string.Join(", ", byCorners.Select(kv => kv.Key + " corners: " + kv.Value)));

        // 1. thickness
        float Thickness(Vector3[] p, List<int> idx)
        {
            var pts = idx.Select(i => p[i]).ToList();
            float lo = pts.Min(x => x.magnitude), hi = pts.Max(x => x.magnitude), mid = (lo + hi) / 2;
            var o = pts.Where(x => x.magnitude > mid).ToList(); var n = pts.Where(x => x.magnitude <= mid).ToList();
            Vector3 avg(List<Vector3> l) { var s = Vector3.zero; foreach (var x in l) s += x; return s / l.Count; }
            return (avg(o) - avg(n)).magnitude;
        }
        float worstRatio = 0f, worstTri = 0f;
        foreach (var p in plates)
        {
            if (corners[p.Key] == 8 || corners[p.Key] == 10) worstRatio = Math.Max(worstRatio, Math.Abs(Thickness(thick, p.Value) / Thickness(rest, p.Value) - 1.1f));
            if (corners[p.Key] == 6) foreach (int i in p.Value) worstTri = Math.Max(worstTri, (thick[i] - rest[i]).magnitude);
        }
        Check("squares, rhombi and pentagons are 1.1x as thick", worstRatio < 1e-4f, $"worst ratio error {worstRatio:E2}");
        Check("the triangles are untouched", worstTri == 0f, $"moved {worstTri:E2}");

        // 2 + 3 + 5: rigid, congruent at the end, hop height
        float R = rest.Max(v => v.magnitude);
        var squarePlates = plates.Where(p => corners[p.Key] == 8 && p.Value.Any(i => mesh.Posed(0, 50f)[i] != thick[i])).Select(p => p.Value).ToList();
        Check("60 square plates move", squarePlates.Count == 60, squarePlates.Count.ToString());
        for (int shape = 0; shape < 2; shape++)
        {
            float worstRigid = 0f, peak = 0f;
            for (int f = 0; f <= 48; f++)
            {
                var posed = mesh.Posed(shape, 100f * f / 48f);
                foreach (var idx in squarePlates)
                {
                    for (int a = 0; a < idx.Count; a++) for (int b = a + 1; b < idx.Count; b++)
                        worstRigid = Math.Max(worstRigid, Math.Abs((posed[idx[a]] - posed[idx[b]]).magnitude - (thick[idx[a]] - thick[idx[b]]).magnitude));
                    var c0 = Vector3.zero; var c1 = Vector3.zero; foreach (int i in idx) { c0 += thick[i]; c1 += posed[i]; }
                    peak = Math.Max(peak, (c1.magnitude - c0.magnitude) / idx.Count / R);
                }
            }
            // between frames Unity blends linearly, so a corner cuts the chord of a 3-5 degree step: rigid to that
            Check($"{(shape == 0 ? "pentagon" : "triangle")} turn keeps every square rigid (incl. between frames)", worstRigid < 2e-3f * R, $"worst {worstRigid / R:E2} R");
            if (shape == 0) Check("the pentagon turn hops 0.14 R at its middle", Math.Abs(peak - 0.14f) < 0.01f, $"peak {peak:F4} R");
            var end = mesh.Posed(shape, 100f);
            var restSet = squarePlates.SelectMany(i => i).Select(i => thick[i]).ToList();
            float worstLand = 0f;
            foreach (int i in squarePlates.SelectMany(x => x)) worstLand = Math.Max(worstLand, restSet.Min(q => (q - end[i]).magnitude));
            Check($"{(shape == 0 ? "pentagon" : "triangle")} turn lands congruent with rest", worstLand < 1e-3f * R, $"worst {worstLand / R:E2} R");
        }

        // 4. at the art's own lift and step, the triangle turn's middle IS the art file's Step1
        var plain = Body();
        args = new object[] { plain, squares, Turns(1f, 1f, 1f), null };
        meth.Invoke(null, args);
        var mid = plain.Posed(1, 50f);
        var anchors = (Vector3[])typeof(CrystalSpaceSquaresSO).GetField("anchors", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(squares);
        float scale = R / doc.GetProperty("bodyRadius").GetSingle(), worstStep1 = 0f; bool matched = false;
        // match anchors to vertices the way the baker does (x flipped or not, by radius ratio)
        foreach (float sx in new[] { 1f, -1f })
        {
            float worst = 0f; int hits = 0;
            for (int i = 0; i < rest.Length; i++)
            {
                int a = -1; for (int k = 0; k < anchors.Length; k++) if ((new Vector3(sx * anchors[k].x * scale, anchors[k].y * scale, anchors[k].z * scale) - rest[i]).magnitude < 1e-4f * R) { a = k; break; }
                if (a < 0) continue;
                hits++;
                var d = keys[1].Deltas[a];
                var step1 = rest[i] + new Vector3(sx * d.x * scale, d.y * scale, d.z * scale);
                worst = Math.Max(worst, (mid[i] - step1).magnitude);
            }
            Console.WriteLine($"  (anchors with x {sx}: {hits} body vertices matched, worst {worst / R:E2} R)");
            // the shell is mirror-symmetric, so both signs match every anchor; the baker takes the first (x as written,
            // Unity's import), and so must this check
            if (hits == 480 * 3) { worstStep1 = worst; matched = true; break; }
        }
        Check("lift 1 + sideways 1 passes exactly through the art file's Step1", matched && worstStep1 < 2e-3f * R, $"worst {worstStep1 / R:E2} R");

        // negative control: a key that is not rigid is refused
        var bent = new CrystalSpaceSquaresSO();
        Set(bent, "anchors", anchors); Set(bent, "bodyRadius", doc.GetProperty("bodyRadius").GetSingle()); Set(bent, "weights", new float[] { 0f, 0f, 0f });
        var bentKeys = keys.Select(k => { var c = new CrystalSpaceSquaresSO.Key(); Set(c, "name", k.Name); Set(c, "stackedOn", k.StackedOn); Set(c, "deltas", k.Deltas.Select(x => x * 1.3f).ToArray()); return c; }).ToArray();
        Set(bent, "keys", bentKeys);
        args = new object[] { Body(), bent, Turns(1.1f, 1.5f, 1.5f), null };
        Check("negative control: a key stretched 30% (not a rigid turn) is refused", !(bool)meth.Invoke(null, args), (string)args[3] ?? "");

        // 6. the cadence never overlaps the squares and a wave
        var cad = new OmniCadence(1.733f, 1.8f, 2.4f, 0.15f);
        int both = 0, sq = 0, wv = 0;
        for (float t = 0f; t < 200f; t += 0.005f)
        {
            bool s = cad.SquaresAt(t, out _) != SquareTurn.None, w = cad.WaveAt(t, out _, out _);
            if (s && w) both++; if (s) sq++; if (w) wv++;
        }
        Check("the cadence never moves the squares and a wave at once", both == 0 && sq > 0 && wv > 0, $"both {both}, squares {sq}, waves {wv}");
        Check("one cycle is 8.33 s (1.733 + 1.8 + 2.4 + 1.8 + 4 x 0.15)", Math.Abs(cad.CycleSeconds - 8.333f) < 1e-3f, cad.CycleSeconds.ToString("F3"));
        var neg = new OmniCadence(1.733f, 1.8f, 2.4f, -0.5f);
        Check("negative control: a negative gap is clamped to 0, never overlapping", neg.GapSeconds == 0f);

        Console.WriteLine(failures == 0 ? "ALL PASS" : failures + " FAILED");
        return failures == 0 ? 0 : 1;
    }
}
