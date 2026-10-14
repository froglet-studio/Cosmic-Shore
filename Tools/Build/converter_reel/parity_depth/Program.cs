// Dumps the SHIPPED depth-prism column maths frame by frame, for parity_depth.cjs to hold src/depthPrisms.js to.
//
//   dotnet run -c Release -- <data/depth.json> <OmniCombineData.json> <out.json>
//
// Shipped code run here (compiled by path, see harness.csproj): DepthRatchet (Seat/Step/RiseTarget/HorizonSealDepth/
// ColumnTop/HiddenBehindBody), CrystalDepthPrismProfileSO (Seat/SealFor, its fields set from the asset's values),
// CrystalDepthPrisms.StepColumns, .ColumnBody and .TryGetPlates (on a Mesh built from data/depth.json's triangles).
// RE-EXPRESSED here because they are private nested members of OmniCombinePlayer (a MonoBehaviour that cannot be built
// headless): DepthColumns.Step (the strength/rest lerp around StepColumns), DepthShape.Of/Morph, and WriteMass's plate-centre
// loop. They are transcribed line for line from OmniCombinePlayer.cs; the JS port is checked against these transcriptions.
//
// Cases (60 fps, 20 s each):
//   triangles          OmniCrystalTriangles' 20 plates, an orbiting + bobbing + zooming eye in the mesh's own space, the
//                      omni's wake ramp as strength, a strike pattern, an eye-at-origin gap (frames 300-302: no step), a
//                      re-fresh at frame 700 and a 0.25 s frame at 900. Shipped profile.
//   triangles-ratchet  the same with RiseWhileHidden and SealAtHorizon OFF and the reseat at -0.6, so reseats (the
//                      two-prism fade) happen every turn.
//   mass               the Mass crystal's 60 plates (OmniCombineData), the crystal turning and flying in, its plates morphing
//                      toward their omni triangle (WriteMass), the shape morphing Mass -> thin triangles, strength = the idle
//                      effect fading out, rest = a MassColumnPlace-like ramp. Eye = camera - crystal centre.
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
    const int Frames = 1200;
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    static int Main(string[] args)
    {
        var depth = JsonDocument.Parse(File.ReadAllText(args[0])).RootElement;
        var combine = JsonDocument.Parse(File.ReadAllText(args[1])).RootElement;
        var sb = new StringBuilder();
        sb.Append('{');

        // ---- the triangles mesh, through the shipped TryGetPlates
        var tri = depth.GetProperty("triangles");
        int nv = tri.GetProperty("vertexCount").GetInt32();
        var posJ = tri.GetProperty("positions").EnumerateArray().Select(e => (float)e.GetDouble()).ToArray();
        var plateJ = tri.GetProperty("plate").EnumerateArray().Select(e => e.GetInt32()).ToArray();
        var pcJ = tri.GetProperty("plateCentres").EnumerateArray().Select(e => (float)e.GetDouble()).ToArray();
        var verts = new Vector3[nv];
        var uv2 = new Vector4[nv];
        for (int i = 0; i < nv; i++)
        {
            verts[i] = new Vector3(posJ[3 * i], posJ[3 * i + 1], posJ[3 * i + 2]);
            int q = plateJ[i];
            uv2[i] = new Vector4(pcJ[3 * q], pcJ[3 * q + 1], pcJ[3 * q + 2], q);
        }
        var mesh = new Mesh { name = "OmniCrystalTriangles", vertices = verts };
        mesh.SetUVs(2, uv2);
        if (!CrystalDepthPrisms.TryGetPlates(mesh, out var dirs, out var centres, out var tops, out var bounds, out float plateRadius, out var problem))
        {
            Console.Error.WriteLine("TryGetPlates refused the mesh: " + problem);
            return 1;
        }
        sb.Append("\"plates\":{\"directions\":").Append(Vecs(dirs)).Append(",\"centres\":").Append(Vecs(centres))
          .Append(",\"tops\":").Append(Floats(tops)).Append(",\"bounds\":").Append(Floats(bounds))
          .Append(",\"plateRadius\":").Append(F(plateRadius)).Append('}');

        // ---- materials and shapes (DepthShape.Of, re-expressed)
        var mats = depth.GetProperty("materials");
        var dj = combine.GetProperty("depth");
        float meshScale = 0f;
        JsonElement massCrystal = default;
        foreach (var c in combine.GetProperty("crystals").EnumerateArray())
            if (c.GetProperty("element").GetString() == "Mass") { massCrystal = c; meshScale = (float)c.GetProperty("meshScale").GetDouble(); }
        var triShape = Shape.Of(mats.GetProperty("omni"), 1f, 1f);
        var massShape = Shape.Of(mats.GetProperty("mass"), meshScale, (float)dj.GetProperty("massThickness").GetDouble());
        var triThin = Shape.Of(mats.GetProperty("omni"), 1f, (float)dj.GetProperty("omniThickness").GetDouble());

        // ---- ColumnBody (shipped) on every plate at a few places
        sb.Append(",\"columnBody\":[");
        float split = triShape.FaceSplit * triShape.SplitUnit;
        float[] places = { 0f, 0.25f, 0.5f, 0.8f, 1f };
        bool first = true;
        for (int j = 0; j < dirs.Length; j++)
            foreach (float p in places)
            {
                CrystalDepthPrisms.ColumnBody(centres[j], bounds[j], p, triShape.Reach, triShape.SizeStart, split, out var bc, out float br);
                sb.Append(first ? "" : ",").Append('[').Append(j).Append(',').Append(F(p)).Append(',').Append(F(bc.x)).Append(',')
                  .Append(F(bc.y)).Append(',').Append(F(bc.z)).Append(',').Append(F(br)).Append(']');
                first = false;
            }
        sb.Append(']');

        // ---- the cases
        var profJ = depth.GetProperty("profile");
        sb.Append(",\"cases\":[");
        sb.Append(TrianglesCase("triangles", Profile(profJ, null), dirs, centres, tops, bounds, plateRadius, triShape));
        sb.Append(',');
        sb.Append(TrianglesCase("triangles-ratchet", Profile(profJ, p => { Set(p, "riseWhileHidden", false); Set(p, "sealAtHorizon", false); Set(p, "reseatDepth", -0.6f); }),
                                dirs, centres, tops, bounds, plateRadius, triShape));
        sb.Append(',');
        sb.Append(MassCase(Profile(profJ, null), massCrystal, massShape, triThin));
        sb.Append("]}");
        File.WriteAllText(args[2], sb.ToString());
        return 0;
    }

    // ------------------------------------------------------------------ cases

    static string TrianglesCase(string name, CrystalDepthPrismProfileSO profile, Vector3[] dirs, Vector3[] centres, float[] tops,
                                float[] bounds, float plateRadius, Shape shape)
    {
        var cols = new DepthColumns(dirs.Length, dirs, centres, tops, bounds, plateRadius);
        var sb = new StringBuilder();
        var stats = new Stats();
        sb.Append("{\"name\":\"").Append(name).Append("\",\"kind\":\"triangles\",\"profile\":").Append(ProfileJson(profile)).Append(",\"frames\":[");
        for (int i = 0; i < Frames; i++)
        {
            double t = i / 60.0;
            float dt = i == 900 ? 0.25f : 1f / 60f;
            double az = 0.55 * t + 0.4 * Math.Sin(0.21 * t), el = 0.5 * Math.Sin(0.37 * t), d = (4.5 + 2.5 * Math.Sin(0.13 * t)) * plateRadius / 1.5;
            var eye = i >= 300 && i <= 302 ? Vector3.zero
                : new Vector3((float)(d * Math.Cos(el) * Math.Sin(az)), (float)(d * Math.Sin(el)), (float)(d * Math.Cos(el) * Math.Cos(az)));
            float strength = (float)Smooth(t / 2.0);
            for (int j = 0; j < cols.Count; j++) cols.Strike[j] = (float)Math.Max(0.0, Math.Sin(3.0 * t + j)) * 0.8f;
            if (i == 700) cols.Fresh = true;
            bool fresh = cols.Fresh;
            var before = (DepthRatchet.Column[])cols.Columns.Clone();
            cols.Step(eye, dt, profile, shape.Reach, shape.SizeStart, strength);
            stats.Count(before, cols.Columns, fresh);
            sb.Append(i == 0 ? "" : ",").Append("{\"eye\":").Append(Vec(eye)).Append(",\"dt\":").Append(F(dt))
              .Append(",\"strength\":").Append(F(strength)).Append(",\"fresh\":").Append(i == 700 ? "true" : "false")
              .Append(",\"strike\":").Append(Floats(cols.Strike)).Append(",\"state\":").Append(State(cols.State, cols.Count)).Append('}');
        }
        sb.Append("],\"stats\":").Append(stats.Json()).Append('}');
        Console.WriteLine($"{name}: {stats.Json()}");
        return sb.ToString();
    }

    static string MassCase(CrystalDepthPrismProfileSO profile, JsonElement mass, Shape massShape, Shape triThin)
    {
        var basis = mass.GetProperty("basis").EnumerateArray().Select(e => (float)e.GetDouble()).ToArray();
        var plateOf = mass.GetProperty("plate").EnumerateArray().Select(e => e.GetInt32()).ToArray();
        var plateSite = mass.GetProperty("plateSite").EnumerateArray().Select(e => e.GetInt32()).ToArray();
        int np = plateSite.Length;
        // OmniCombinePlayer.Build's site groups, re-expressed.
        var bySite = new Dictionary<int, List<int>>();
        for (int q = 0; q < np; q++)
        {
            if (!bySite.TryGetValue(plateSite[q], out var list)) bySite[plateSite[q]] = list = new List<int>();
            list.Add(q);
        }
        var sites = new int[np][];
        foreach (var group in bySite.Values) foreach (int q in group) sites[q] = group.ToArray();
        var cols = new DepthColumns(np);
        var sum = new Vector3[np];
        var count = new int[np];
        var plates = new Vector4[np];
        var morph = new float[np];
        int nv = plateOf.Length;
        var pose = new float[3 * nv];

        float r0 = 0f;
        for (int q = 0; q < np; q++) { var c = Vector3.zero; int n = 0; for (int i = 0; i < nv; i++) if (plateOf[i] == q) { c = c + new Vector3(basis[3 * i], basis[3 * i + 1], basis[3 * i + 2]); n++; } r0 += (c / n).magnitude; }
        r0 /= np;

        var sb = new StringBuilder();
        var stats = new Stats();
        sb.Append("{\"name\":\"mass\",\"kind\":\"mass\",\"profile\":").Append(ProfileJson(profile)).Append(",\"frames\":[");
        for (int fr = 0; fr < Frames; fr++)
        {
            double t = fr / 60.0;
            float dt = 1f / 60f;
            // The crystal's motion (inputs, dumped): a turn about a fixed axis, a flight in to the origin, the morph.
            double ang = 0.4 * t, ax = 0.3, ay = 1.0, az = 0.2, al = Math.Sqrt(ax * ax + ay * ay + az * az);
            ax /= al; ay /= al; az /= al;
            double cs = Math.Cos(ang), sn = Math.Sin(ang), oc = 1 - cs;
            var M = new[] {
                (float)(cs + ax * ax * oc), (float)(ax * ay * oc - az * sn), (float)(ax * az * oc + ay * sn),
                (float)(ay * ax * oc + az * sn), (float)(cs + ay * ay * oc), (float)(ay * az * oc - ax * sn),
                (float)(az * ax * oc - ay * sn), (float)(az * ay * oc + ax * sn), (float)(cs + az * az * oc) };
            double fly = 1 - Smooth((t - 8) / 6);
            var centre = new Vector3((float)(1.5 * r0 * Math.Cos(0.3 * t) * fly), (float)(0.3 * r0 * Math.Sin(0.5 * t) * fly), (float)(1.5 * r0 * Math.Sin(0.3 * t) * fly));
            float m = (float)Smooth((t - 8) / 8);
            float strength = (float)(1 - Smooth((t - 6) / 2));
            float rest = (float)(1 - 0.85 * (1 - Smooth((t - 14) / 3)));
            for (int i = 0; i < nv; i++)
            {
                float bx = basis[3 * i], by = basis[3 * i + 1], bz = basis[3 * i + 2];
                pose[3 * i] = M[0] * bx + M[1] * by + M[2] * bz + centre.x;
                pose[3 * i + 1] = M[3] * bx + M[4] * by + M[5] * bz + centre.y;
                pose[3 * i + 2] = M[6] * bx + M[7] * by + M[8] * bz + centre.z;
            }
            for (int q = 0; q < np; q++) morph[q] = Mathf.Clamp01(m * 1.5f - q / 120f);

            // ---- WriteMass, re-expressed line for line (OmniCombinePlayer.cs)
            Array.Clear(sum, 0, sum.Length);
            Array.Clear(count, 0, count.Length);
            for (int i = 0; i < plateOf.Length; i++)
            {
                sum[plateOf[i]] += new Vector3(pose[3 * i], pose[3 * i + 1], pose[3 * i + 2]) - centre;
                count[plateOf[i]]++;
            }
            float morphMean = 0f;
            for (int q = 0; q < np; q++)
            {
                var own = sum[q] / Mathf.Max(1, count[q]);
                var site = Vector3.zero;
                int n = 0;
                foreach (int s in sites[q])
                {
                    site += sum[s];
                    n += count[s];
                }
                site /= Mathf.Max(1, n);
                var c = Vector3.Lerp(own, site, morph[q]);
                plates[q] = new Vector4(c.x, c.y, c.z, q);
                morphMean += morph[q];
            }
            morphMean /= Mathf.Max(1, np);
            var shape = Shape.Morph(massShape, triThin, morphMean);
            float radius = 0f;
            for (int q = 0; q < np; q++)
            {
                var c = new Vector3(plates[q].x, plates[q].y, plates[q].z);
                cols.Centres[q] = c;
                cols.Directions[q] = c.normalized;
                cols.Tops[q] = shape.TopHeight;
                radius += c.magnitude;
            }
            cols.PlateRadius = radius / Mathf.Max(1, np);
            double caz = 0.5 * t, cel = 0.35 * Math.Sin(0.29 * t), cd = (3.0 + 1.5 * Math.Sin(0.17 * t)) * r0;
            var cam = new Vector3((float)(cd * Math.Cos(cel) * Math.Sin(caz)), (float)(cd * Math.Sin(cel)), (float)(cd * Math.Cos(cel) * Math.Cos(caz)));
            var eye = cam - centre;   // piece.Renderer.transform.InverseTransformPoint(cameraWorld): the piece sits at `centre`
            bool fresh = cols.Fresh;
            var before = (DepthRatchet.Column[])cols.Columns.Clone();
            cols.Step(eye, dt, profile, shape.Reach, shape.SizeStart, strength, rest);
            stats.Count(before, cols.Columns, fresh);

            sb.Append(fr == 0 ? "" : ",").Append("{\"M\":").Append(Floats(M)).Append(",\"centre\":").Append(Vec(centre))
              .Append(",\"cam\":").Append(Vec(cam)).Append(",\"m\":").Append(F(m)).Append(",\"dt\":").Append(F(dt))
              .Append(",\"strength\":").Append(F(strength)).Append(",\"rest\":").Append(F(rest))
              .Append(",\"plates\":[");
            for (int q = 0; q < np; q++) sb.Append(q == 0 ? "" : ",").Append(F(plates[q].x)).Append(',').Append(F(plates[q].y)).Append(',').Append(F(plates[q].z));
            sb.Append("],\"plateRadius\":").Append(F(cols.PlateRadius)).Append(",\"topHeight\":").Append(F(shape.TopHeight))
              .Append(",\"morphMean\":").Append(F(morphMean)).Append(",\"state\":").Append(State(cols.State, cols.Count)).Append('}');
        }
        sb.Append("],\"stats\":").Append(stats.Json()).Append('}');
        Console.WriteLine($"mass: {stats.Json()}");
        return sb.ToString();
    }

    // ------------------------------------------------------------------ OmniCombinePlayer's private members, re-expressed

    /// <summary>OmniCombinePlayer.DepthColumns, line for line (Columns exposed for the branch counts).</summary>
    sealed class DepthColumns
    {
        public readonly Vector3[] Directions, Centres;
        public readonly float[] Tops, Bounds, Strike;
        public float PlateRadius;
        public readonly Vector4[] State = new Vector4[CrystalDepthPrisms.MaxPlates];
        public bool Fresh = true;
        public readonly DepthRatchet.Column[] Columns;

        public DepthColumns(int plates) : this(plates, new Vector3[plates], new Vector3[plates], new float[plates], new float[plates], 0f) { }

        public DepthColumns(int plates, Vector3[] directions, Vector3[] centres, float[] tops, float[] bounds, float plateRadius)
        {
            Columns = new DepthRatchet.Column[plates];
            Directions = directions;
            Centres = centres;
            Tops = tops;
            Bounds = bounds;
            Strike = new float[plates];
            PlateRadius = plateRadius;
        }

        public int Count => Columns.Length;

        public void Step(Vector3 eye, float dt, CrystalDepthPrismProfileSO profile, float reach, float sizeAtTop, float strength, float rest = 1f)
        {
            if (eye.sqrMagnitude < 1e-8f || PlateRadius <= 0f) return;
            CrystalDepthPrisms.StepColumns(Columns, Directions, Centres, Tops, PlateRadius, eye, dt, Fresh, profile, reach, sizeAtTop);
            for (int j = 0; j < Columns.Length; j++)
            {
                var c = Columns[j];
                State[j] = new Vector4(Mathf.Lerp(rest, c.Place, strength), Mathf.Lerp(1f, c.Fade, strength),
                                       Mathf.Lerp(1f, c.LeavingPlace, strength), Strike[j]);
            }
            Fresh = false;
        }
    }

    /// <summary>OmniCombinePlayer.DepthShape, line for line (material floats read from data/depth.json).</summary>
    readonly struct Shape
    {
        public readonly float Thickness, TopHeight, SplitUnit, Reach, FaceSplit, SizeStart;
        Shape(float thickness, float topHeight, float splitUnit, float reach, float faceSplit, float sizeStart)
        {
            Thickness = thickness; TopHeight = topHeight; SplitUnit = splitUnit; Reach = reach; FaceSplit = faceSplit; SizeStart = sizeStart;
        }
        static float G(JsonElement m, string n) => (float)m.GetProperty(n).GetDouble();
        public static Shape Of(JsonElement m, float unit, float baked) => new(
            G(m, "_Thickness") * baked, G(m, "_TopHeight") * unit, G(m, "_SplitUnit") * unit,
            G(m, "_Reach"), G(m, "_FaceSplit"), G(m, "_PlateScaleStart"));
        public static Shape Morph(in Shape a, in Shape b, float t) => new(
            Mathf.Lerp(a.Thickness, b.Thickness, t), Mathf.Lerp(a.TopHeight, b.TopHeight, t),
            Mathf.Lerp(a.SplitUnit, b.SplitUnit, t), a.Reach, a.FaceSplit, a.SizeStart);
    }

    // ------------------------------------------------------------------ helpers

    sealed class Stats
    {
        int follow, rise, reseat, fading, fresh, hold;
        public void Count(DepthRatchet.Column[] a, DepthRatchet.Column[] b, bool wasFresh)
        {
            if (wasFresh) { fresh += b.Length; return; }
            for (int j = 0; j < b.Length; j++)
            {
                if (a[j].Fade < 1f) fading++;
                else if (b[j].Fade == 0f) reseat++;
                else if (b[j].Place > a[j].Place) follow++;
                else if (b[j].Place < a[j].Place) rise++;
                else hold++;
            }
        }
        public string Json() => $"{{\"fresh\":{fresh},\"follow\":{follow},\"rise\":{rise},\"reseat\":{reseat},\"fading\":{fading},\"hold\":{hold}}}";
    }

    static CrystalDepthPrismProfileSO Profile(JsonElement j, Action<CrystalDepthPrismProfileSO> tweak)
    {
        var p = ScriptableObject.CreateInstance<CrystalDepthPrismProfileSO>();
        foreach (var prop in j.EnumerateObject())
        {
            if (prop.Value.ValueKind == JsonValueKind.True || prop.Value.ValueKind == JsonValueKind.False) Set(p, prop.Name, prop.Value.GetBoolean());
            else Set(p, prop.Name, (float)prop.Value.GetDouble());
        }
        tweak?.Invoke(p);
        return p;
    }

    static void Set(CrystalDepthPrismProfileSO p, string field, object value)
    {
        var fi = typeof(CrystalDepthPrismProfileSO).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)
                 ?? throw new Exception($"CrystalDepthPrismProfileSO has no field '{field}'");
        fi.SetValue(p, value);
    }

    static string ProfileJson(CrystalDepthPrismProfileSO p) =>
        $"{{\"openDepth\":{F(p.OpenDepth)},\"sealDepth\":{F(p.SealDepth)},\"profilePower\":{F(p.ProfilePower)},\"reseatDepth\":{F(p.ReseatDepth)}," +
        $"\"reseatFadeSeconds\":{F(p.ReseatFadeSeconds)},\"sealAtHorizon\":{(p.SealAtHorizon ? "true" : "false")},\"horizonMargin\":{F(p.HorizonMargin)}," +
        $"\"riseWhileHidden\":{(p.RiseWhileHidden ? "true" : "false")},\"riseRate\":{F(p.RiseRate)},\"occluderRadius\":{F(p.OccluderRadius)}," +
        $"\"orientationJitter\":{F(p.OrientationJitter)}}}";

    static double Smooth(double x) { x = x < 0 ? 0 : x > 1 ? 1 : x; return x * x * (3 - 2 * x); }
    static string F(float v) => v.ToString("R", Inv);
    static string Vec(Vector3 v) => $"[{F(v.x)},{F(v.y)},{F(v.z)}]";
    static string Vecs(Vector3[] a) => "[" + string.Join(",", a.Select(Vec)) + "]";
    static string Floats(float[] a) => "[" + string.Join(",", a.Select(F)) + "]";
    static string State(Vector4[] s, int n)
    {
        var sb = new StringBuilder("[");
        for (int j = 0; j < n; j++) sb.Append(j == 0 ? "" : ",").Append(F(s[j].x)).Append(',').Append(F(s[j].y)).Append(',').Append(F(s[j].z)).Append(',').Append(F(s[j].w));
        return sb.Append(']').ToString();
    }
}
