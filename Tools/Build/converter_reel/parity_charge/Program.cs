// The reference side of the Charge light parity (Tools/Build/converter_reel/parity_charge.cjs compares the JS port
// src/chargeArcs.js against what this writes). Compiles the SHIPPED ChargeArcs.cs and CrystalChargeArcsProfileSO.cs
// against the omni turns harness's UnityStub.cs (nothing under Assets/ is copied or edited) and, on the shipped combine
// data, does exactly what OmniCombinePlayer does:
//   - settings: CrystalChargeArcsProfileSO.Settings, its private fields set from CrystalChargeArcsProfile.asset;
//   - BuildChargeLight: ChargeSites.TryBuild on the Charge crystal's basis (thickness 1) and on the omni's verts at the
//     cadence's plateThickness (OmniCrystalCadence.asset);
//   - WriteSeamWalk: walker(seed 7, start 0).Advance(t), SeamWalk, then ArcLayer.Upload's faces (own centre) - 60 fps x 30 s;
//   - WriteFlux: chain(seed 7 ^ 0x5bd1e995), Flux, Upload's faces (site centre), then FluxStrikes both overloads on a
//     fixed set of 40 prisms - 60 fps x 30 s;
// with a fixed eye in each layer's space. Writes <out>/ref.json (settings, sites, per-frame counts, hashes, light[] and
// strikes) and <out>/frames.bin (every FullEvery-th frame's writer arrays in full).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using UnityEngine;

static class Program
{
    const int Fps = 60, Seconds = 30, FullEvery = 30, StrikeCount = 40;
    const int Seed = 7;
    static readonly Vector3 ChargeEye = new(0.9f, 1.4f, -4.6f);
    static readonly Vector3 OmniEye = new(1.3f, 2.1f, -6.4f);

    static string R(float f) => f.ToString("R", CultureInfo.InvariantCulture);
    static string Arr(IEnumerable<float> a) => "[" + string.Join(",", a.Select(R)) + "]";
    static string V(Vector3 v) => $"[{R(v.x)},{R(v.y)},{R(v.z)}]";
    static string Vs(IEnumerable<Vector3> a) => "[" + string.Join(",", a.Select(V)) + "]";
    static string Is(IEnumerable<int> a) => "[" + string.Join(",", a) + "]";

    static uint Bits(float f) => f == 0f ? 0u : (uint)BitConverter.SingleToInt32Bits(f);
    static uint Fnv(uint h, uint w) { unchecked { return (h ^ w) * 16777619u; } }
    static uint HashV3(List<Vector3> a) { uint h = 2166136261u; foreach (var v in a) { h = Fnv(h, Bits(v.x)); h = Fnv(h, Bits(v.y)); h = Fnv(h, Bits(v.z)); } return h; }
    static uint HashV4(List<Vector4> a) { uint h = 2166136261u; foreach (var v in a) { h = Fnv(h, Bits(v.x)); h = Fnv(h, Bits(v.y)); h = Fnv(h, Bits(v.z)); h = Fnv(h, Bits(v.w)); } return h; }
    static uint HashI(List<int> a) { uint h = 2166136261u; foreach (int i in a) h = Fnv(h, (uint)i); return h; }
    static uint HashF(float[] a, int n) { uint h = 2166136261u; for (int i = 0; i < n; i++) h = Fnv(h, Bits(a[i])); return h; }

    static Dictionary<string, string> AssetFields(string path)
    {
        var d = new Dictionary<string, string>();
        foreach (var line in File.ReadAllLines(path))
        {
            var m = System.Text.RegularExpressions.Regex.Match(line, @"^  ([A-Za-z_]\w*): (-?[0-9.eE+-]+)\s*$");
            if (m.Success) d[m.Groups[1].Value] = m.Groups[2].Value;
        }
        return d;
    }

    static Vector3[] Vectors(JsonElement flat)
    {
        var f = flat.EnumerateArray().Select(e => e.GetSingle()).ToArray();
        var v = new Vector3[f.Length / 3];
        for (int i = 0; i < v.Length; i++) v[i] = new Vector3(f[3 * i], f[3 * i + 1], f[3 * i + 2]);
        return v;
    }

    static int Main(string[] args)
    {
        string root = Path.GetFullPath(args.Length > 0 ? args[0] : "../../../..");
        string outDir = Path.GetFullPath(args.Length > 1 ? args[1] : "out");
        Directory.CreateDirectory(outDir);
        var json = new StringBuilder();
        json.Append("{");

        // settings, exactly as the profile SO builds them
        var profileFields = AssetFields(Path.Combine(root, "Assets/_SO_Assets/Environment/CrystalChargeArcsProfile.asset"));
        var so = new CrystalChargeArcsProfileSO();
        foreach (var field in typeof(CrystalChargeArcsProfileSO).GetFields(BindingFlags.NonPublic | BindingFlags.Instance))
        {
            if (!profileFields.TryGetValue(field.Name, out var s)) throw new Exception($"profile asset has no {field.Name}");
            field.SetValue(so, field.FieldType == typeof(int) ? int.Parse(s, CultureInfo.InvariantCulture) : (object)float.Parse(s, CultureInfo.InvariantCulture));
        }
        var settings = so.Settings;
        json.Append("\"settings\":{");
        json.Append(string.Join(",", typeof(ChargeArcSettings).GetFields(BindingFlags.Public | BindingFlags.Instance)
            .Select(f => $"\"{f.Name}\":" + (f.FieldType == typeof(int) ? f.GetValue(settings).ToString() : R((float)f.GetValue(settings))))));
        json.Append($",\"Span\":{R(settings.Span)}}}");
        float plateThickness = float.Parse(AssetFields(Path.Combine(root, "Assets/_SO_Assets/Environment/OmniCrystalCadence.asset"))["plateThickness"], CultureInfo.InvariantCulture);
        json.Append($",\"plateThickness\":{R(plateThickness)}");

        // the sites, as BuildChargeLight builds them
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "Assets/_SO_Assets/Environment/OmniCombine/OmniCombineData.json")));
        var charge = doc.RootElement.GetProperty("crystals").EnumerateArray().First(c => c.GetProperty("element").GetString() == "Charge");
        var omni = doc.RootElement.GetProperty("omni");
        var chargeVerts = Vectors(charge.GetProperty("basis"));
        var omniVerts = Vectors(omni.GetProperty("verts"));
        var chargeTris = charge.GetProperty("tris").EnumerateArray().Select(e => e.GetInt32()).ToArray();
        var omniTris = omni.GetProperty("tris").EnumerateArray().Select(e => e.GetInt32()).ToArray();
        json.Append($",\"inputHash\":{{\"charge\":{HashV3(chargeVerts.ToList())},\"omni\":{HashV3(omniVerts.ToList())}}}");
        if (!ChargeSites.TryBuild(chargeVerts, chargeTris, 1f, out var chargeSites, out var problem)) { Console.Error.WriteLine("Charge: " + problem); return 1; }
        if (!ChargeSites.TryBuild(omniVerts, omniTris, plateThickness, out var omniSites, out problem)) { Console.Error.WriteLine("omni: " + problem); return 1; }
        json.Append(",\"sites\":{\"charge\":").Append(Sites(chargeSites)).Append(",\"omni\":").Append(Sites(omniSites)).Append("}");

        // a few flux paths, sampled
        json.Append(",\"paths\":[");
        var pairs = new[] { (0, 3), (5, 9), (11, 2), (7, 1) };
        json.Append(string.Join(",", pairs.Select(p =>
        {
            var path = omniSites.PathOf(p.Item1, p.Item2);
            var pts = new List<Vector3>();
            for (int q = 0; q < 5; q++) for (int i = 0; i <= 24; i++) pts.Add(path.Strand(q, i / 24f, settings.FluxFork, settings.FluxBundle * omniSites.Radius, settings.FluxTurns));
            return $"{{\"source\":{p.Item1},\"sink\":{p.Item2},\"centre\":{Vs(Enumerable.Range(0, 9).Select(i => path.Centre(i / 8f)))},\"strands\":{Vs(pts)},\"sourceOffsets\":{Vs(path.SourceOffsets)},\"sinkOffsets\":{Vs(path.SinkOffsets)}}}";
        })));
        json.Append("]");

        // the noise
        json.Append(",\"noise\":{\"x\":[");
        var xs = Enumerable.Range(0, 200).Select(i => -37.3f + i * 0.4137f).ToArray();
        json.Append(string.Join(",", xs.Select(R))).Append("],\"fbm3\":").Append(Arr(xs.Select(x => ChargeArcs.Fbm(x, 3))));
        json.Append(",\"hash\":").Append(Arr(Enumerable.Range(-50, 100).Select(i => ChargeArcs.Hash(i * 7919, 977))));
        var rnd = new System.Random(Seed ^ 0x5bd1e995);
        json.Append(",\"random\":[").Append(string.Join(",", Enumerable.Range(0, 50).Select(i => rnd.Next(1000)))).Append("]");
        // Large and negative seeds: the legacy seeding's int arithmetic wraps (a JS port in doubles must wrap too).
        json.Append(",\"randomSeeds\":[");
        var seeds = new[] { 287815223 ^ 0x5bd1e995, 899190242 ^ 0x5bd1e995, int.MaxValue, int.MinValue, -12345, 1552075790 };
        json.Append(string.Join(",", seeds.Select(sd => { var r = new System.Random(sd); return "[" + string.Join(",", Enumerable.Range(0, 200).Select(i => r.Next(1000))) + "]"; })));
        json.Append("]}");

        using var bin = new BinaryWriter(File.Create(Path.Combine(outDir, "frames.bin")));
        int frames = Fps * Seconds;

        // the seam walk
        {
            var walker = new ChargeSeamWalker(chargeSites, Seed, 0f);
            var writer = new ChargeArcMeshWriter();
            var light = new float[chargeSites.Faces.Length];
            json.Append(",\"seam\":{\"eye\":").Append(V(ChargeEye)).Append(",\"frames\":[");
            for (int i = 0; i < frames; i++)
            {
                float t = (float)(i / (double)Fps);
                walker.Advance(t, settings);
                int points = ChargeArcs.SeamWalk(chargeSites, walker, t, settings, writer, ChargeEye, light);
                for (int f = 0; f < chargeSites.Faces.Length; f++) writer.Face(chargeSites.Faces[f], light[f], true);
                if (i > 0) json.Append(",");
                json.Append($"{{\"t\":{R(t)},\"points\":{points},\"legs\":{walker.Legs.Count},\"nv\":{writer.Positions.Count},\"ni\":{writer.Indices.Count},")
                    .Append($"\"hp\":{HashV3(writer.Positions)},\"hd\":{HashV4(writer.Data)},\"hi\":{HashI(writer.Indices)},\"light\":{Arr(light)}}}");
                if (i % FullEvery == 0) Dump(bin, 0, i, writer);
                writer.Clear();
            }
            json.Append("]}");
        }

        // the flux arcs and the strikes
        {
            var chain = new ChargeFluxChain(omniSites, Seed ^ 0x5bd1e995);
            var writer = new ChargeArcMeshWriter();
            var light = new float[omniSites.Faces.Length];
            float R0 = omniSites.Radius;
            var centres = new Vector3[StrikeCount];
            var radii = new float[StrikeCount];
            var places = new float[StrikeCount];
            var seats = new Vector3[StrikeCount];
            var seatRadii = new float[StrikeCount];
            for (int j = 0; j < StrikeCount; j++)
            {
                // a Fibonacci sphere of prisms, at four heights and three sizes, at seven column places
                float y = 1f - 2f * (j + 0.5f) / StrikeCount, rr = Mathf.Sqrt(1f - y * y), phi = j * 2.3999632f;
                var dir = new Vector3(rr * Mathf.Cos(phi), y, rr * Mathf.Sin(phi));
                seats[j] = dir * (R0 * 0.92f);
                centres[j] = dir * (R0 * (1f + 0.12f * (j % 4)));
                radii[j] = R0 * (0.06f + 0.03f * (j % 3));
                seatRadii[j] = R0 * 0.05f;
                places[j] = (j % 7) / 6f;
            }
            var strikeA = new float[StrikeCount];
            var strikeB = new float[StrikeCount];
            json.Append(",\"flux\":{\"seed\":").Append(Seed ^ 0x5bd1e995).Append(",\"eye\":").Append(V(OmniEye))
                .Append($",\"strikeInputs\":{{\"centres\":{Vs(centres)},\"radii\":{Arr(radii)},\"places\":{Arr(places)},\"seats\":{Vs(seats)},\"seatRadii\":{Arr(seatRadii)}}}")
                .Append(",\"gates\":").Append(Arr(Enumerable.Range(0, 21).Select(i => ChargeArcs.StrikeGate(i / 20f, settings))))
                .Append(",\"frames\":[");
            int struckFrames = 0;
            for (int i = 0; i < frames; i++)
            {
                float t = (float)(i / (double)Fps);
                int drawn = ChargeArcs.Flux(omniSites, chain, t, settings, writer, OmniEye, light);
                for (int f = 0; f < omniSites.Faces.Length; f++) writer.Face(omniSites.Faces[f], light[f], false);
                ChargeArcs.FluxStrikes(omniSites, chain, t, settings, centres, radii, StrikeCount, strikeA);
                ChargeArcs.FluxStrikes(omniSites, chain, t, settings, centres, radii, places, seats, seatRadii, StrikeCount, strikeB);
                if (strikeA.Any(v => v > 0f)) struckFrames++;
                if (i > 0) json.Append(",");
                json.Append($"{{\"t\":{R(t)},\"drawn\":{drawn},\"nv\":{writer.Positions.Count},\"ni\":{writer.Indices.Count},")
                    .Append($"\"hp\":{HashV3(writer.Positions)},\"hd\":{HashV4(writer.Data)},\"hi\":{HashI(writer.Indices)},\"light\":{Arr(light)},")
                    .Append($"\"strike\":{Arr(strikeA)},\"strikeGated\":{Arr(strikeB)}}}");
                if (i % FullEvery == 0) Dump(bin, 1, i, writer);
                writer.Clear();
            }
            json.Append("]}");
            Console.WriteLine($"flux: {struckFrames} of {frames} frames strike at least one of the {StrikeCount} prisms");
        }
        json.Append("}");
        File.WriteAllText(Path.Combine(outDir, "ref.json"), json.ToString());
        Console.WriteLine($"wrote {Path.Combine(outDir, "ref.json")} and frames.bin ({frames} frames per effect, full every {FullEvery})");
        return 0;
    }

    static void Dump(BinaryWriter bin, int effect, int frame, ChargeArcMeshWriter w)
    {
        bin.Write(effect); bin.Write(frame);
        bin.Write(w.Positions.Count); bin.Write(w.Indices.Count);
        foreach (var p in w.Positions) { bin.Write(p.x); bin.Write(p.y); bin.Write(p.z); }
        foreach (var d in w.Data) { bin.Write(d.x); bin.Write(d.y); bin.Write(d.z); bin.Write(d.w); }
        foreach (int i in w.Indices) bin.Write(i);
    }

    static string Sites(ChargeSites s)
    {
        var b = new StringBuilder();
        b.Append($"{{\"kind\":{(int)s.Kind},\"radius\":{R(s.Radius)},\"axes\":{Vs(s.Axes)},");
        b.Append("\"neighbours\":[" + string.Join(",", s.Neighbours.Select(Is)) + "],");
        b.Append($"\"junctions\":{Vs(s.Junctions)},\"junctionSites\":[" + string.Join(",", s.JunctionSites.Select(Is)) + "],");
        b.Append("\"seams\":[" + string.Join(",", s.Seams.Select(e =>
            $"{{\"a\":{e.A},\"b\":{e.B},\"siteA\":{e.SiteA},\"siteB\":{e.SiteB},\"length\":{R(e.Length)},\"points\":{Vs(e.Points)}}}")) + "],");
        b.Append("\"seamsOfJunction\":[" + string.Join(",", s.SeamsOfJunction.Select(Is)) + "],");
        b.Append("\"faces\":[" + string.Join(",", s.Faces.Select(f =>
            $"{{\"site\":{f.Site},\"ring\":{Vs(f.Ring)},\"centre\":{V(f.Centre)},\"own\":{Arr(f.OwnFalloff)},\"siteFalloff\":{Arr(f.SiteFalloff)},\"atCentre\":{R(f.SiteFalloffAtCentre)},\"back\":{Vs(f.Back)},\"sigma\":{R(f.Sigma)}}}")) + "],");
        b.Append("\"facesOfSite\":[" + string.Join(",", s.FacesOfSite.Select(Is)) + "],");
        b.Append("\"corners\":[" + string.Join(",", s.Corners.Select(Vs)) + "]}");
        return b.ToString();
    }
}
