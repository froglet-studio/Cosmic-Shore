// THE PRISM CLIP AUDIT: run every shipped Spawnable shape the way its prefab authors it, and measure
// whether any prism it lays interpenetrates another (Docs/ECOSYSTEM.md §49.10 and §62, "no prism may
// interpenetrate another"). Driven by Tools/Build/audit_prism_clipping.py, which reads the prefabs
// and writes the spec this reads - run THAT, not this.
//
// The rule is held at AUTHORING time: a generator emits flush prisms, nothing resolves clipping while
// the game loads. So this measures exactly what a generator emits (clamped into its prism prefab's
// scale window, as the lay clamps it) and, to tell the generator's author WHERE the fix belongs,
// tags every lay with the FAMILY that emitted it:
//
//   a cell environment (CellEnvironmentSpawnableBase.Emit) - the generator methods on the call stack
//                                                            above Emit, e.g. "BuildRings>Ring"
//   any other shape                                        - its trail, "trail 3"
//
// and reports, per family, the pairs inside it, the largest UNIFORM scale at which it is flush with
// itself (a tiling or a rail whose prisms are simply too big for their spacing), and which families
// cross which (the structural clips a scale cannot fix without gutting the shape).
//
// A prism is its oriented box: centre, rotation, full extents = its lay scale. Two boxes
// interpenetrate when the 15-axis separating-axis test finds no axis that separates them.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using UnityEngine;

static class ClipAudit
{
    /// <summary>The prism prefab's serialized scale window for the entry being run, applied to every
    /// leaf lay the way PrismScaleAnimator.SetTargetScale applies it; null = the shape admits its
    /// authored scale (CellEnvironmentSpawnableBase.AdmitsAuthoredPrismScale).</summary>
    static (Vector3 min, Vector3 max)? s_window;

    public static Vector3 Clamp(Vector3 s) => s_window is { } w
        ? new Vector3(Mathf.Clamp(s.x, w.min.x, w.max.x), Mathf.Clamp(s.y, w.min.y, w.max.y), Mathf.Clamp(s.z, w.min.z, w.max.z))
        : s;

    // ── family tags, in lay order ───────────────────────────────────────────────────────────────

    static readonly List<string> s_tags = new();

    /// <summary>Off while baking: the tag walks the stack per lay, which would swamp the timing.</summary>
    static bool s_tagging = true;

    /// <summary>Called by the harness's CellEnvironmentSpawnableBase.Emit (run.sh points it here)
    /// for every lay: the first two generator methods above Emit name the family.</summary>
    public static void TagEmit()
    {
        if (!s_tagging) return;
        var frames = new StackTrace(2, false).GetFrames();
        var names = new List<string>(2);
        foreach (var f in frames)
        {
            var m = f.GetMethod();
            if (m == null) continue;
            var t = m.DeclaringType;
            if (t == null || t == typeof(ClipAudit) || t.Name == "CellEnvironmentSpawnableBase" ||
                t.Name == "Program" || t.Namespace == "System" || t.Name == "SpawnableBase") continue;
            string n = m.Name;
            int lt = n.IndexOf('<'), gt = n.IndexOf('>');
            if (lt >= 0 && gt > lt) n = n.Substring(lt + 1, gt - lt - 1);   // a local function's host
            if (names.Count == 0 || names[^1] != n) names.Add(n);
            if (names.Count == 2) break;
        }
        names.Reverse();
        s_tags.Add(names.Count == 0 ? "?" : string.Join(">", names));
    }

    /// <summary>Called by the harness's GenerateForHarness on every generation (run.sh points it
    /// here): a leaf shape's lays are tagged by trail; a composite's points are placements, not
    /// lays; a cell environment tagged its own through Emit.</summary>
    public static SpawnTrailData[] Hook(SpawnableBase gen, SpawnTrailData[] trails)
    {
        if (trails == null || gen.AuditIsComposite || gen is CellEnvironmentSpawnableBase) return trails;
        string owner = gen.GetType().Name.Replace("Spawnable", "");
        for (int t = 0; t < trails.Length; t++)
            if (trails[t]?.Points != null)
                for (int i = 0; i < trails[t].Points.Length; i++) s_tags.Add($"{owner} trail {t}");
        return trails;
    }

    // ── boxes ───────────────────────────────────────────────────────────────────────────────────

    readonly struct Box
    {
        public readonly Vector3 C, X, Y, Z;
        public readonly float Hx, Hy, Hz, Radius;
        public Box(SpawnPoint p, float k = 1f)
        {
            C = p.Position;
            X = p.Rotation * Vector3.right; Y = p.Rotation * Vector3.up; Z = p.Rotation * Vector3.forward;
            Hx = 0.5f * k * Math.Abs(p.Scale.x); Hy = 0.5f * k * Math.Abs(p.Scale.y); Hz = 0.5f * k * Math.Abs(p.Scale.z);
            Radius = new Vector3(Hx, Hy, Hz).magnitude;
        }
        public float MinHalf => Math.Min(Hx, Math.Min(Hy, Hz));
    }

    static double Project(in Box o, Vector3 L) =>
        Math.Abs(Vector3.Dot(o.X, L)) * o.Hx + Math.Abs(Vector3.Dot(o.Y, L)) * o.Hy + Math.Abs(Vector3.Dot(o.Z, L)) * o.Hz;

    /// <summary>Signed separation: POSITIVE clears by that much, NEGATIVE is the penetration depth.</summary>
    static double Separation(in Box a, in Box b)
    {
        Vector3 d = b.C - a.C;
        double best = double.NegativeInfinity;
        Span<Vector3> f = stackalloc Vector3[6] { a.X, a.Y, a.Z, b.X, b.Y, b.Z };
        for (int i = 0; i < 6; i++) best = Math.Max(best, Test(a, b, d, f[i]));
        for (int i = 0; i < 3; i++)
            for (int j = 3; j < 6; j++) best = Math.Max(best, Test(a, b, d, Vector3.Cross(f[i], f[j])));
        return best;
    }

    static double Test(in Box a, in Box b, Vector3 d, Vector3 L)
    {
        double len = Math.Sqrt((double)L.x * L.x + (double)L.y * L.y + (double)L.z * L.z);
        if (len < 1e-6) return double.NegativeInfinity;
        return (Math.Abs(Vector3.Dot(d, L)) - Project(a, L) - Project(b, L)) / len;
    }

    /// <summary>The pair interpenetrates (beyond a float epsilon of the thinner prism). Two faces
    /// that TOUCH are flush, and flush is the goal.</summary>
    static bool Clips(in Box a, in Box b) =>
        Separation(a, b) < -1e-3 * Math.Max(1e-6, Math.Min(a.MinHalf, b.MinHalf)) - 1e-5;

    // ── the run ─────────────────────────────────────────────────────────────────────────────────

    static int Main(string[] args)
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        if (args.Length > 1 && args[0] == "--bake") return Bake(args[1]);
        var spec = JsonDocument.Parse(File.ReadAllText(args[0])).RootElement;

        var program = typeof(ClipAudit).Assembly.GetType("Program");
        var matrixType = program.GetNestedType("Matrix", BindingFlags.NonPublic);
        var identity = matrixType.GetProperty("Identity", BindingFlags.Public | BindingFlags.Static).GetValue(null);
        var run = program.GetMethod("RunGenerator", BindingFlags.NonPublic | BindingFlags.Static);

        using var stdout = Console.OpenStandardOutput();
        using var w = new Utf8JsonWriter(stdout, new JsonWriterOptions { Indented = true });
        w.WriteStartArray();
        foreach (var entry in spec.EnumerateArray())
        {
            string name = entry.GetProperty("name").GetString();
            s_window = entry.TryGetProperty("window", out var win) && win.ValueKind == JsonValueKind.Object
                ? (V(win.GetProperty("min")), V(win.GetProperty("max"))) : null;

            int faults = Harness.Faults;
            s_tags.Clear();
            var lays = new List<(SpawnPoint, Domains, PrismKind)>();
            try
            {
                if (entry.TryGetProperty("course", out var course)) RunPosed(entry.GetProperty("node"), course, lays);
                else run.Invoke(null, new object[] { entry.GetProperty("node"), identity, lays, null });
            }
            catch (TargetInvocationException e)
            {
                Console.Error.WriteLine($"[clip] {name}: generator threw {e.InnerException?.GetType().Name}: {e.InnerException?.Message}");
                Harness.Faults++;
            }
            var tags = s_tags.Count == lays.Count ? s_tags.ToList() : Enumerable.Repeat("untagged", lays.Count).ToList();

            Write(w, name, lays, tags, Harness.Faults != faults);
            w.Flush();
        }
        w.WriteEndArray();
        w.Flush();
        return 0;
    }

    static void Write(Utf8JsonWriter w, string name, List<(SpawnPoint p, Domains d, PrismKind k)> lays, List<string> tags, bool faulted)
    {
        var boxes = lays.Select(l => new Box(l.p)).ToList();
        var pairs = ClippingPairs(boxes);

        w.WriteStartObject();
        w.WriteString("name", name);
        w.WriteNumber("prisms", boxes.Count);
        w.WriteBoolean("faulted", faulted);
        w.WriteNumber("clipping", pairs.Count);
        w.WriteNumber("volume", Math.Round(lays.Sum(l => (double)Math.Abs(l.p.Scale.x * l.p.Scale.y * l.p.Scale.z)), 1));
        int dup = pairs.Count(pr => (boxes[pr.j].C - boxes[pr.i].C).magnitude <
                                    0.02 * Math.Min(boxes[pr.i].MinHalf, boxes[pr.j].MinHalf));
        w.WriteNumber("duplicates", dup);
        double worst = pairs.Count > 0 ? pairs.Max(pr => pr.depth) : 0;
        w.WriteNumber("worst_depth", Math.Round(worst, 4));

        // Families: inside each, and across.
        var famOf = tags;
        var families = famOf.Distinct().OrderBy(s => s).ToList();
        w.WriteStartArray("families");
        foreach (var fam in families)
        {
            var idx = Enumerable.Range(0, lays.Count).Where(i => famOf[i] == fam).ToList();
            var inner = pairs.Where(pr => famOf[pr.i] == fam && famOf[pr.j] == fam).ToList();
            w.WriteStartObject();
            w.WriteString("family", fam);
            w.WriteNumber("prisms", idx.Count);
            w.WriteNumber("inner_pairs", inner.Count);
            w.WriteNumber("inner_coincident", inner.Count(pr => (boxes[pr.j].C - boxes[pr.i].C).magnitude <
                                                                 0.02 * Math.Min(boxes[pr.i].MinHalf, boxes[pr.j].MinHalf)));
            // The largest uniform scale at which this family is flush with itself (about each
            // prism's own centre), or 0 when coincident centres make that impossible.
            w.WriteNumber("inner_flush_scale", inner.Count == 0 ? 1.0 : Math.Round(FlushScale(lays, inner), 4));
            w.WriteEndObject();
        }
        w.WriteEndArray();

        var cross = pairs.Where(pr => famOf[pr.i] != famOf[pr.j])
                         .GroupBy(pr => string.CompareOrdinal(famOf[pr.i], famOf[pr.j]) < 0
                             ? (famOf[pr.i], famOf[pr.j]) : (famOf[pr.j], famOf[pr.i]))
                         .OrderByDescending(g => g.Count());
        w.WriteStartArray("crossings");
        foreach (var g in cross)
        {
            w.WriteStartObject();
            w.WriteString("a", g.Key.Item1);
            w.WriteString("b", g.Key.Item2);
            w.WriteNumber("pairs", g.Count());
            w.WriteNumber("worst_depth", Math.Round(g.Max(pr => pr.depth), 3));
            w.WriteNumber("flush_scale", Math.Round(FlushScale(lays, g.ToList()), 4));
            w.WriteEndObject();
        }
        w.WriteEndArray();
        w.WriteEndObject();
    }

    /// <summary>Largest k in (0,1] such that every listed pair clears when BOTH prisms are scaled by k
    /// about their own centres; 0 when no k does (coincident centres).</summary>
    static double FlushScale(List<(SpawnPoint p, Domains d, PrismKind k)> lays, List<(int i, int j, double depth)> pairs)
    {
        bool AllClear(float k)
        {
            foreach (var (i, j, _) in pairs)
                if (Clips(new Box(lays[i].p, k), new Box(lays[j].p, k))) return false;
            return true;
        }
        if (!AllClear(1e-3f)) return 0;
        float lo = 1e-3f, hi = 1f;
        for (int s = 0; s < 20; s++)
        {
            float mid = 0.5f * (lo + hi);
            if (AllClear(mid)) lo = mid; else hi = mid;
        }
        return lo;
    }

    /// <summary>Every interpenetrating pair, by sweep-and-prune on the widest axis then the 15-axis test.</summary>
    static List<(int i, int j, double depth)> ClippingPairs(List<Box> boxes)
    {
        int n = boxes.Count;
        var lo = new Vector3[n]; var hi = new Vector3[n];
        Vector3 cmin = Vector3.positiveInfinity, cmax = Vector3.negativeInfinity;
        for (int i = 0; i < n; i++)
        {
            var b = boxes[i];
            var e = new Vector3(
                Math.Abs(b.X.x) * b.Hx + Math.Abs(b.Y.x) * b.Hy + Math.Abs(b.Z.x) * b.Hz,
                Math.Abs(b.X.y) * b.Hx + Math.Abs(b.Y.y) * b.Hy + Math.Abs(b.Z.y) * b.Hz,
                Math.Abs(b.X.z) * b.Hx + Math.Abs(b.Y.z) * b.Hy + Math.Abs(b.Z.z) * b.Hz);
            lo[i] = b.C - e; hi[i] = b.C + e;
            cmin = Vector3.Min(cmin, b.C); cmax = Vector3.Max(cmax, b.C);
        }
        var spread = cmax - cmin;
        int ax = spread.x >= spread.y && spread.x >= spread.z ? 0 : spread.y >= spread.z ? 1 : 2;
        var order = Enumerable.Range(0, n).OrderBy(i => lo[i][ax]).ToArray();
        var pairs = new List<(int, int, double)>();
        for (int s = 0; s < n; s++)
        {
            int i = order[s];
            for (int t = s + 1; t < n; t++)
            {
                int j = order[t];
                if (lo[j][ax] > hi[i][ax]) break;
                if (lo[i].x > hi[j].x || lo[j].x > hi[i].x || lo[i].y > hi[j].y || lo[j].y > hi[i].y ||
                    lo[i].z > hi[j].z || lo[j].z > hi[i].z) continue;
                if (!Clips(boxes[i], boxes[j])) continue;
                pairs.Add((Math.Min(i, j), Math.Max(i, j), -Separation(boxes[i], boxes[j])));
            }
        }
        return pairs;
    }

    /// <summary>
    /// A generator that lays nothing until its controller POSES it (SpawnableBreakwater: the
    /// controller generates the course from the match seed and calls SetCourse before Spawn). The
    /// course is made here by the shipped BreakwaterCourse at the entry's intensity and seed, with
    /// the settings the controller supplies, then handed over the same way.
    /// </summary>
    static void RunPosed(JsonElement node, JsonElement course, List<(SpawnPoint, Domains, PrismKind)> sink)
    {
        var program = typeof(ClipAudit).Assembly.GetType("Program");
        var setField = program.GetMethod("SetField", BindingFlags.NonPublic | BindingFlags.Static);
        var findField = program.GetMethod("FindField", BindingFlags.NonPublic | BindingFlags.Static);
        var convert = program.GetMethod("Convert", BindingFlags.NonPublic | BindingFlags.Static);
        var type = typeof(ClipAudit).Assembly.GetTypes().First(t => t.Name == node.GetProperty("type").GetString());
        var gen = (SpawnableBase)Activator.CreateInstance(type);
        foreach (var f in node.GetProperty("fields").EnumerateObject())
            setField.Invoke(null, new object[] { gen, f.Name, f.Value });
        int intensity = course.GetProperty("intensity").GetInt32(), seed = course.GetProperty("seed").GetInt32();
        if (course.TryGetProperty("switchback", out var sb))
        {
            // SpawnableSwitchbackFeatures: SwitchbackController generates the Switchback course and hands the
            // generator the fun type's closed-form layout (SetLayout). Posed on the shipped shell the EditMode
            // tests use, at the picker's fixed world seed.
            var funType = (SwitchbackFunType)sb.GetProperty("funType").GetInt32();
            var cs = SwitchbackCourseSettings.ForIntensity(intensity, funType);
            cs.GateCount = sb.GetProperty("gates").GetInt32();
            cs.InnerRadius = (float)sb.GetProperty("inner").GetDouble();
            cs.OuterRadius = (float)sb.GetProperty("outer").GetDouble();
            cs.FirstGateDirection = Vector3.up;
            cs.FirstGateDistance = (float)sb.GetProperty("firstGateDistance").GetDouble();
            float clear = (float)sb.GetProperty("clearRadius").GetDouble();
            var gates = SwitchbackCourse.Generate(seed, cs);
            var layout = SwitchbackBlastRun.Layout(gates, intensity, Vector3.zero, clear);
            type.GetMethod("SetLayout").Invoke(gen, new object[] { gates, intensity, funType, layout, Vector3.zero, clear });
            gen.GenerateForHarness();
            foreach (var lay in ((CellEnvironmentSpawnableBase)gen).CachedLays)
                sink.Add((new SpawnPoint(lay.Point.Position, lay.Point.Rotation, Clamp(lay.Point.Scale)), lay.Domain, lay.Kind));
            return;
        }
        object settings = BreakwaterCourseSettings.ForIntensity(intensity);
        if (course.TryGetProperty("settings", out var over))
            foreach (var f in over.EnumerateObject())
            {
                var fi = (FieldInfo)findField.Invoke(null, new object[] { typeof(BreakwaterCourseSettings), f.Name });
                fi.SetValue(settings, convert.Invoke(null, new object[] { fi.FieldType, f.Value, f.Name }));
            }
        var stations = BreakwaterCourse.Generate(seed, (BreakwaterCourseSettings)settings);
        type.GetMethod("SetCourse").Invoke(gen, new object[] { stations });
        gen.GenerateForHarness();
        foreach (var lay in ((CellEnvironmentSpawnableBase)gen).CachedLays)
            sink.Add((new SpawnPoint(lay.Point.Position, lay.Point.Rotation, Clamp(lay.Point.Scale)), lay.Domain, lay.Kind));
    }

    // ── baking (Tools/Build/bake_prism_layouts.py) ──────────────────────────────────────────────

    /// <summary>
    /// Write the layout bake of every cell environment in the spec (entries: name, node, source, out) with
    /// the SHIPPED PrismLayoutBakeFormat, so the fingerprint and bytes are exactly what the game
    /// reads. Each world is generated twice: the first pass warms the JIT, the second is timed (the
    /// load cost the bake removes). The bake is decoded back and compared bit for bit before it is
    /// reported, and the decode is timed too (the load cost that is left).
    /// </summary>
    static int Bake(string specPath)
    {
        s_tagging = false;
        var spec = JsonDocument.Parse(File.ReadAllText(specPath)).RootElement;
        var program = typeof(ClipAudit).Assembly.GetType("Program");
        var setField = program.GetMethod("SetField", BindingFlags.NonPublic | BindingFlags.Static);
        var findField = program.GetMethod("FindField", BindingFlags.NonPublic | BindingFlags.Static);

        CellEnvironmentSpawnableBase Make(JsonElement node)
        {
            var type = typeof(ClipAudit).Assembly.GetTypes().First(t => t.Name == node.GetProperty("type").GetString());
            var gen = (CellEnvironmentSpawnableBase)Activator.CreateInstance(type);
            foreach (var f in node.GetProperty("fields").EnumerateObject())
                setField.Invoke(null, new object[] { gen, f.Name, f.Value });
            ((FieldInfo)findField.Invoke(null, new object[] { type, "rng" })).SetValue(gen, new System.Random(1));
            return gen;
        }

        using var stdout = Console.OpenStandardOutput();
        using var w = new Utf8JsonWriter(stdout, new JsonWriterOptions { Indented = true });
        w.WriteStartArray();
        int failures = 0;
        foreach (var entry in spec.EnumerateArray())
        {
            string name = entry.GetProperty("name").GetString();
            var node = entry.GetProperty("node");
            Make(node).GenerateForHarness();
            var gen = Make(node);
            var clock = Stopwatch.StartNew();
            gen.GenerateForHarness();
            double generateMs = clock.Elapsed.TotalMilliseconds;

            ulong fingerprint = PrismLayoutBakeFormat.Fingerprint(gen);
            ulong sourceHash = PrismLayoutBakeFormat.SourceHash(File.ReadAllText(entry.GetProperty("source").GetString()));
            var lays = gen.CachedLays;
            var sites = gen.PlantingSites ?? new List<FloraPlantingSite>();
            byte[] data = PrismLayoutBakeFormat.Encode(fingerprint, sourceHash, lays, sites);
            File.WriteAllBytes(entry.GetProperty("out").GetString(), data);

            PrismLayoutBakeFormat.TryDecode(data, fingerprint, out _, out _);   // warm
            clock.Restart();
            bool ok = PrismLayoutBakeFormat.TryDecode(data, fingerprint, out var back, out var backSites);
            double decodeMs = clock.Elapsed.TotalMilliseconds;
            string mismatch = !ok ? "did not decode" : back.Count != lays.Count ? "lay count" :
                backSites.Count != sites.Count ? "site count" : null;
            for (int i = 0; mismatch == null && i < lays.Count; i++)
            {
                var a = lays[i]; var b = back[i];
                if (!Same(a.Point.Position, b.Point.Position) || !Same(a.Point.Scale, b.Point.Scale) ||
                    a.Point.Rotation.x != b.Point.Rotation.x || a.Point.Rotation.y != b.Point.Rotation.y ||
                    a.Point.Rotation.z != b.Point.Rotation.z || a.Point.Rotation.w != b.Point.Rotation.w ||
                    a.Domain != b.Domain || a.Kind != b.Kind)
                    mismatch = $"lay {i}";
            }
            for (int i = 0; mismatch == null && i < sites.Count; i++)
                if (!Same(sites[i].Position, backSites[i].Position) || sites[i].Kind != backSites[i].Kind ||
                    (sites[i].Up - backSites[i].Up).magnitude > 1e-6f)
                    mismatch = $"site {i}";
            if (mismatch != null) { failures++; Console.Error.WriteLine($"[bake] {name}: round trip failed at {mismatch}"); }

            w.WriteStartObject();
            w.WriteString("name", name);
            w.WriteString("type", gen.GetType().Name);
            w.WriteString("fingerprint", fingerprint.ToString("x16"));
            w.WriteString("stem", PrismLayoutBakeFormat.FileStem(gen.GetType(), fingerprint));
            w.WriteNumber("prisms", lays.Count);
            w.WriteNumber("sites", sites.Count);
            w.WriteNumber("bytes", data.Length);
            w.WriteNumber("generate_ms", Math.Round(generateMs, 2));
            w.WriteNumber("decode_ms", Math.Round(decodeMs, 2));
            w.WriteBoolean("round_trip", mismatch == null);
            w.WriteEndObject();
            w.Flush();
        }
        w.WriteEndArray();
        w.Flush();
        return failures == 0 ? 0 : 1;
    }

    static bool Same(Vector3 a, Vector3 b) => a.x == b.x && a.y == b.y && a.z == b.z;

    static Vector3 V(JsonElement e) { var a = e.EnumerateArray().Select(x => (float)x.GetDouble()).ToArray(); return new Vector3(a[0], a[1], a[2]); }
}
