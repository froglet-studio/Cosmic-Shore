// THE PRISM CLIP AUDIT: run every shipped Spawnable shape the way its prefab authors it, and measure
// whether any prism it lays interpenetrates another (Docs/ECOSYSTEM.md §49.10, "no prism may
// interpenetrate another"). Driven by Tools/Build/audit_prism_clipping.py, which reads the prefabs
// and writes the spec this reads - run THAT, not this.
//
// A prism is its oriented box: centre, rotation, full extents = its lay scale (the same body every
// fit_* / verify_* tool and the garland harness measure). Two boxes interpenetrate when the
// 15-axis separating-axis test finds no axis that separates them; the signed separation is
// reported, so a pair that clears by a hair is visible as well as one that is buried.
//
// The generator itself is run through the card-art harness's own RunGenerator (compiled in beside
// this file), so a shape is posed by exactly the code that photographs it - one reading of a prefab,
// not two.
using System;
using System.Collections.Generic;
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

    public static Vector3 Clamp(Vector3 s)
    {
        // A zero scale is a CLEARED lay once the pass is in (PrismTrailBuilder skips it); before
        // it, the clamp grew an authored zero to the floor and laid it.
        if (!s_resolve) s_authoredVolume += Math.Abs((double)s.x * s.y * s.z);
        if (s.x == 0f && s.y == 0f && s.z == 0f)
        {
            if (s_resolve) return s;
            s_authoredZero++;
        }
        return s_window is { } w
            ? new Vector3(Mathf.Clamp(s.x, w.min.x, w.max.x), Mathf.Clamp(s.y, w.min.y, w.max.y), Mathf.Clamp(s.z, w.min.z, w.max.z))
            : s;
    }

    /// <summary>True for the resolved run: every generation goes through the shipped pass.</summary>
    static bool s_resolve;
    static int s_shrunk, s_cleared;

    /// <summary>
    /// Called by the harness's GenerateForHarness on EVERY generation (run.sh points it here), so a
    /// nested structure is resolved node by node exactly as the game resolves it: a leaf on its
    /// own, a composite never. Mirrors SpawnableBase.GetTrailData + ResolveClearance, and
    /// CellEnvironmentSpawnableBase.ResolveClearance for the lay-list family.
    /// </summary>
    public static SpawnTrailData[] Hook(SpawnableBase gen, SpawnTrailData[] trails)
    {
        if (!s_resolve || gen.AuditIsComposite || !ResolvesInGame(gen) || trails == null) return trails;
        s_resolvedNodes++;
        PrismClearance.Result r;
        if (gen is CellEnvironmentSpawnableBase env)
            r = env.AuditResolve(trails, s_window?.min, s_window?.max);
        else
        {
            var all = trails.Where(t => t?.Points != null).SelectMany(t => t.Points).ToArray();
            r = PrismClearance.Resolve(all, s_window?.min, s_window?.max);
            int k = 0;
            foreach (var t in trails)
            {
                if (t?.Points == null) continue;
                Array.Copy(all, k, t.Points, 0, t.Points.Length);
                k += t.Points.Length;
            }
        }
        s_shrunk += r.Shrunk;
        s_cleared += r.Cleared;
        return trails;
    }

    /// <summary>Whether the game runs the pass for this shape: the lay-list family always, any other
    /// shape when it names the prism it lays (overrides ClearancePrism - the shim's own base
    /// returns null, which in the game means "my leaves are not prisms").</summary>
    static bool ResolvesInGame(SpawnableBase gen)
    {
        if (gen is CellEnvironmentSpawnableBase) return true;
        var prop = gen.GetType().GetProperty("ClearancePrism", BindingFlags.Instance | BindingFlags.NonPublic);
        return prop != null && prop.DeclaringType != typeof(SpawnableBase);
    }

    static bool s_faulted, s_resolvedAny;
    static double s_resolveMs;

    /// <summary>One generation of an entry, raw or through the pass. The resolved run's counters
    /// and timing are left in the statics for the writer.</summary>
    static List<(SpawnPoint, Domains, PrismKind)> Run(MethodInfo run, JsonElement entry, object identity, string name, bool resolve)
    {
        int faults = Harness.Faults;
        s_resolve = resolve;
        s_shrunk = s_cleared = s_resolvedNodes = 0;
        if (!resolve) { s_authoredZero = 0; s_authoredVolume = 0; }
        var lays = new List<(SpawnPoint, Domains, PrismKind)>();
        var sw = System.Diagnostics.Stopwatch.StartNew();
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
        if (resolve)
        {
            s_resolveMs = sw.Elapsed.TotalMilliseconds;
            s_resolvedAny = s_resolvedNodes > 0;
        }
        else s_faulted = Harness.Faults != faults;
        s_resolve = false;
        return lays;
    }

    static int s_resolvedNodes, s_authoredZero;
    /// <summary>Sum of the AUTHORED (unclamped) lay volumes of the raw run - the convention every
    /// budget model prices a baseline in.</summary>
    static double s_authoredVolume;

    /// <summary>
    /// A generator that lays nothing until its controller POSES it (SpawnableBreakwater: the
    /// controller generates the course from the match seed and calls SetCourse before Spawn). The
    /// course is made here by the shipped BreakwaterCourse at the entry's intensity and seed, then
    /// handed over the same way, so the audit sees the arena a match would build.
    /// </summary>
    static void RunPosed(JsonElement node, JsonElement course, List<(SpawnPoint, Domains, PrismKind)> sink)
    {
        var program = typeof(ClipAudit).Assembly.GetType("Program");
        var setField = program.GetMethod("SetField", BindingFlags.NonPublic | BindingFlags.Static);
        var type = typeof(ClipAudit).Assembly.GetTypes().First(t => t.Name == node.GetProperty("type").GetString());
        var gen = (SpawnableBase)Activator.CreateInstance(type);
        foreach (var f in node.GetProperty("fields").EnumerateObject())
            setField.Invoke(null, new object[] { gen, f.Name, f.Value });
        int intensity = course.GetProperty("intensity").GetInt32(), seed = course.GetProperty("seed").GetInt32();
        // The settings the controller supplies on top of ForIntensity (the shell off the cell's
        // nucleus, the station count off EndConditionOverrides), as the card-art recipe derives them.
        object settings = BreakwaterCourseSettings.ForIntensity(intensity);
        if (course.TryGetProperty("settings", out var over))
        {
            var findField = program.GetMethod("FindField", BindingFlags.NonPublic | BindingFlags.Static);
            var convert = program.GetMethod("Convert", BindingFlags.NonPublic | BindingFlags.Static);
            foreach (var f in over.EnumerateObject())
            {
                var fi = (FieldInfo)findField.Invoke(null, new object[] { typeof(BreakwaterCourseSettings), f.Name });
                fi.SetValue(settings, convert.Invoke(null, new object[] { fi.FieldType, f.Value, f.Name }));
            }
        }
        var stations = BreakwaterCourse.Generate(seed, (BreakwaterCourseSettings)settings);
        type.GetMethod("SetCourse").Invoke(gen, new object[] { stations });
        gen.GenerateForHarness();
        foreach (var lay in ((CellEnvironmentSpawnableBase)gen).CachedLays)
            sink.Add((new SpawnPoint(lay.Point.Position, lay.Point.Rotation, Clamp(lay.Point.Scale)), lay.Domain, lay.Kind));
    }

    static Vector3 V(JsonElement e) { var a = e.EnumerateArray().Select(x => (float)x.GetDouble()).ToArray(); return new Vector3(a[0], a[1], a[2]); }

    readonly struct Box
    {
        public readonly Vector3 C, X, Y, Z;
        public readonly float Hx, Hy, Hz, Radius;
        public Box(SpawnPoint p)
        {
            C = p.Position;
            X = p.Rotation * Vector3.right; Y = p.Rotation * Vector3.up; Z = p.Rotation * Vector3.forward;
            Hx = 0.5f * Math.Abs(p.Scale.x); Hy = 0.5f * Math.Abs(p.Scale.y); Hz = 0.5f * Math.Abs(p.Scale.z);
            Radius = new Vector3(Hx, Hy, Hz).magnitude;
        }
        public float MinHalf => Math.Min(Hx, Math.Min(Hy, Hz));
    }

    static double Project(in Box o, Vector3 L) =>
        Math.Abs(Vector3.Dot(o.X, L)) * o.Hx + Math.Abs(Vector3.Dot(o.Y, L)) * o.Hy + Math.Abs(Vector3.Dot(o.Z, L)) * o.Hz;

    /// <summary>Signed separation: POSITIVE clears by that much, NEGATIVE is the penetration depth
    /// along the axis that separates them least badly.</summary>
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

    static int Main(string[] args)
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
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
            var lays = Run(run, entry, identity, name, false);
            var resolved = Run(run, entry, identity, name, true);

            var boxes = lays.Select(l => new Box(l.Item1)).ToList();
            var r = Measure(boxes);
            var kept = resolved.Where(l => !PrismClearance.IsCleared(l.Item1)).Select(l => new Box(l.Item1)).ToList();
            var after = Measure(kept);
            double volBefore = lays.Sum(l => (double)Math.Abs(l.Item1.Scale.x * l.Item1.Scale.y * l.Item1.Scale.z));
            double volAfter = resolved.Sum(l => (double)Math.Abs(l.Item1.Scale.x * l.Item1.Scale.y * l.Item1.Scale.z));
            var pts = resolved.Select(l => l.Item1).ToArray();

            // Before/after lay lists for the renderer (audit_prism_clipping.py --render): the
            // card-art "prisms" layer row, [x,y,z, qx,qy,qz,qw, sx,sy,sz, domain, kind].
            if (entry.TryGetProperty("rows", out var rowsPath) && rowsPath.ValueKind == JsonValueKind.String)
            {
                using var f = File.Create(rowsPath.GetString());
                using var rw = new Utf8JsonWriter(f);
                rw.WriteStartObject();
                foreach (var (key, set) in new[] { ("before", lays.Select(l => l.Item1).ToArray()), ("after", pts) })
                {
                    rw.WriteStartArray(key);
                    for (int k = 0; k < set.Length; k++)
                    {
                        var p = set[k];
                        if (PrismClearance.IsCleared(p)) continue;
                        rw.WriteStartArray();
                        foreach (float v in new[] { p.Position.x, p.Position.y, p.Position.z, p.Rotation.x, p.Rotation.y, p.Rotation.z,
                                     p.Rotation.w, p.Scale.x, p.Scale.y, p.Scale.z })
                            rw.WriteNumberValue(Math.Round(v, 4));
                        rw.WriteNumberValue((int)lays[k].Item2);
                        rw.WriteNumberValue((int)lays[k].Item3);
                        rw.WriteEndArray();
                    }
                    rw.WriteEndArray();
                }
                rw.WriteEndObject();
            }
            w.WriteStartObject();
            w.WriteString("name", name);
            w.WriteNumber("prisms", boxes.Count);
            w.WriteBoolean("faulted", s_faulted);
            w.WriteBoolean("resolves_in_game", s_resolvedAny);
            // Lays the generator AUTHORED at scale zero: the clamp used to grow them to its floor
            // and lay them; a zero scale now means "not laid" (PrismClearance.IsCleared).
            w.WriteNumber("authored_zero", s_authoredZero);
            // The cell's baseline before and after (Cell.LiveVolume counts every laid prism): the
            // authored count and volume a budget model prices, and what the game actually lays.
            w.WriteNumber("authored_count", lays.Count);
            w.WriteNumber("authored_volume", Math.Round(s_authoredVolume, 1));
            w.WriteNumber("laid_count", kept.Count);
            w.WriteNumber("laid_volume", Math.Round(volAfter, 1));
            w.WriteNumber("near", r.near);
            w.WriteNumber("clipping", r.clipping);
            w.WriteNumber("clipping_adjacent", r.adjacent);
            w.WriteNumber("deep", r.deep);
            w.WriteNumber("duplicates", r.dup);
            w.WriteNumber("worst_depth", Math.Round(r.worst, 4));
            w.WriteNumber("worst_depth_rel", Math.Round(r.worstRel, 4));
            w.WriteNumber("resolved_shrunk", s_shrunk);
            w.WriteNumber("resolved_cleared", s_cleared);
            w.WriteNumber("resolved_clipping", after.clipping);
            w.WriteNumber("resolved_worst_depth", Math.Round(after.worst, 5));
            if (after.wi >= 0)
            {
                var keptPts = resolved.Where(l => !PrismClearance.IsCleared(l.Item1)).Select(l => l.Item1).ToList();
                w.WriteStartArray("resolved_worst_pair");
                foreach (int k in new[] { after.wi, after.wj })
                {
                    var p = keptPts[k];
                    w.WriteStartObject();
                    w.WriteNumber("index", k);
                    w.WriteString("pos", $"{p.Position.x:F3},{p.Position.y:F3},{p.Position.z:F3}");
                    w.WriteString("rot", $"{p.Rotation.x:F4},{p.Rotation.y:F4},{p.Rotation.z:F4},{p.Rotation.w:F4}");
                    w.WriteString("scale", $"{p.Scale.x:F4},{p.Scale.y:F4},{p.Scale.z:F4}");
                    w.WriteEndObject();
                }
                w.WriteEndArray();
            }
            w.WriteNumber("resolved_volume_kept", volBefore > 0 ? Math.Round(volAfter / volBefore, 4) : 1);
            w.WriteNumber("generate_and_resolve_ms", Math.Round(s_resolveMs, 1));
            if (r.wi >= 0)
            {
                w.WriteStartArray("worst_pair");
                foreach (int k in new[] { r.wi, r.wj })
                {
                    var p = lays[k].Item1;
                    w.WriteStartObject();
                    w.WriteNumber("index", k);
                    w.WriteString("pos", $"{p.Position.x:F2},{p.Position.y:F2},{p.Position.z:F2}");
                    w.WriteString("scale", $"{p.Scale.x:F3},{p.Scale.y:F3},{p.Scale.z:F3}");
                    w.WriteEndObject();
                }
                w.WriteEndArray();
            }
            w.WriteEndObject();
            w.Flush();
        }
        w.WriteEndArray();
        w.Flush();
        return 0;
    }

    /// <summary>
    /// Every pair whose bounding spheres meet goes through the 15-axis test. A pair is CLIPPING when
    /// it interpenetrates by more than a float epsilon of the smaller prism, and DEEP when the depth
    /// is more than a quarter of the smaller prism's thinnest half-extent - the depth at which a
    /// player reads one prism as passing through the other rather than as two touching.
    /// "Adjacent" is the consecutive pair in lay order (one trail's neighbours), reported apart
    /// because a trail laid with no gap clips its own neighbour and that is one structural fix.
    /// </summary>
    static (int near, int clipping, int adjacent, int deep, double worst, double worstRel, int wi, int wj, int dup) Measure(List<Box> boxes)
    {
        float cell = 1f;
        foreach (var b in boxes) cell = Math.Max(cell, 2f * b.Radius);
        var grid = new Dictionary<(int, int, int), List<int>>();
        (int, int, int) Key(Vector3 p) => ((int)Math.Floor(p.x / cell), (int)Math.Floor(p.y / cell), (int)Math.Floor(p.z / cell));
        for (int i = 0; i < boxes.Count; i++)
        {
            var k = Key(boxes[i].C);
            if (!grid.TryGetValue(k, out var list)) grid[k] = list = new List<int>();
            list.Add(i);
        }
        int near = 0, clipping = 0, adjacent = 0, deep = 0, wi = -1, wj = -1, dup = 0;
        double worst = 0, worstRel = 0;
        for (int i = 0; i < boxes.Count; i++)
        {
            var (kx, ky, kz) = Key(boxes[i].C);
            for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            for (int dz = -1; dz <= 1; dz++)
            {
                if (!grid.TryGetValue((kx + dx, ky + dy, kz + dz), out var list)) continue;
                foreach (int j in list)
                {
                    if (j <= i) continue;
                    var a = boxes[i]; var b = boxes[j];
                    if ((b.C - a.C).magnitude > a.Radius + b.Radius) continue;
                    near++;
                    double sep = Separation(a, b);
                    double thin = Math.Max(1e-6, Math.Min(a.MinHalf, b.MinHalf));
                    if (sep >= -1e-4 * thin - 1e-5) continue;
                    clipping++;
                    if (j == i + 1) adjacent++;
                    // A DUPLICATE: the same prism laid twice (centres within a hundredth of the
                    // thinner prism) - one structural fix, dedupe, not a clearance problem.
                    if ((b.C - a.C).magnitude < 0.02 * thin) dup++;
                    double rel = -sep / thin;
                    if (rel > 0.25) deep++;
                    if (-sep > worst) { worst = -sep; worstRel = rel; wi = i; wj = j; }
                }
            }
        }
        return (near, clipping, adjacent, deep, worst, worstRel, wi, wj, dup);
    }
}
