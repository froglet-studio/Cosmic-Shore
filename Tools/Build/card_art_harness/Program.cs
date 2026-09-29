// Card-art harness entry point: read a render spec, RUN the shipped generators it names, turn what
// they emit into a scene, and write one raw RGB image per card.
//
// The spec is written by Tools/Build/render_card_backgrounds.py, which is the only thing that knows
// which arena a card's mode builds at intensity 2 and how that arena's prefab is authored. This file
// knows nothing about modes: it knows how to run a generator, how to lay out a course, and how to
// draw. So a new mode is a row in the driver's table, never an edit here - unless it brings a new
// KIND of source (a new course family), which is then one case in BuildLayer.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using CardArt;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using UnityEngine;

static class Program
{
    sealed class Palette
    {
        public readonly Dictionary<(int dom, int kind), Material> Cache = new();
        public readonly Dictionary<int, (V3 b, V3 r)[]> Tiers = new(); // domain -> [plain, shielded, super]
        public V3 Danger;
        public Material For(Domains d, PrismKind k)
        {
            if (Cache.TryGetValue(((int)d, (int)k), out var m)) return m;
            if (!Tiers.TryGetValue((int)d, out var t)) t = Tiers[(int)Domains.Blue];
            (V3 b, V3 r) pair = k switch
            {
                PrismKind.Shielded => t[1],
                PrismKind.SuperShielded => t[2],
                // Danger has no colour fields of its own: the domain's SHIELDED base face under
                // the shared, domain-independent danger rim (Docs/PALETTE.md §4).
                PrismKind.Danger => (t[1].b, Danger),
                _ => t[0],
            };
            m = new Material { Base = pair.b, Rim = pair.r };
            Cache[((int)d, (int)k)] = m;
            return m;
        }
    }

    static int Main(string[] args)
    {
        if (args.Length < 2) { Console.Error.WriteLine("usage: cardart <spec.json> <out-dir> [card-name ...]"); return 2; }
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        var spec = JsonDocument.Parse(File.ReadAllText(args[0])).RootElement;
        string outDir = args[1];
        Directory.CreateDirectory(outDir);
        var only = new HashSet<string>(args.Skip(2));

        var palette = ReadPalette(spec.GetProperty("palette"));
        var stats = new List<string>();
        foreach (var card in spec.GetProperty("cards").EnumerateArray())
        {
            string name = card.GetProperty("name").GetString();
            if (only.Count > 0 && !only.Contains(name)) continue;
            int faultsBefore = UnityEngine.Harness.Faults;
            var scene = new Scene();
            int prisms = 0;
            foreach (var layer in card.GetProperty("layers").EnumerateArray())
                prisms += BuildLayer(layer, scene, palette);
            if (card.TryGetProperty("stage", out var stage))
                Stage(stage, scene, palette);
            if (UnityEngine.Harness.Faults != faultsBefore)
            {
                Console.Error.WriteLine($"[cardart] {name}: a generator reported an error - refusing to photograph it");
                return 1;
            }
            if (scene.TriangleCount == 0) { Console.Error.WriteLine($"[cardart] {name}: empty scene"); return 1; }

            int w = card.GetProperty("width").GetInt32(), h = card.GetProperty("height").GetInt32();
            int ss = card.TryGetProperty("supersample", out var sse) ? sse.GetInt32() : 3;
            var cam = BuildCamera(card.GetProperty("camera"), scene, (double)w / h);
            var look = ReadLook(card.TryGetProperty("look", out var le) ? le : default);
            var pixels = Renderer.Render(scene, cam, look, w, h, ss);
            File.WriteAllBytes(Path.Combine(outDir, name + ".rgb"), pixels);
            stats.Add($"{{\"name\":\"{name}\",\"prisms\":{prisms},\"triangles\":{scene.TriangleCount}}}");
            Console.Error.WriteLine($"[cardart] {name}: {prisms} prisms, {scene.TriangleCount} triangles");
        }
        File.WriteAllText(Path.Combine(outDir, "stats.json"), "[" + string.Join(",", stats) + "]");
        return 0;
    }

    // ── layers ────────────────────────────────────────────────────────────────────────────────

    static int BuildLayer(JsonElement layer, Scene scene, Palette pal)
    {
        string kind = layer.GetProperty("kind").GetString();
        switch (kind)
        {
            case "generator":
            {
                var lays = new List<(SpawnPoint p, Domains d, PrismKind k)>();
                RunGenerator(layer, Matrix.Identity, lays);
                foreach (var (p, d, k) in lays) AddPrism(scene, p, pal.For(d, k));
                return lays.Count;
            }
            case "switchback":
            case "headlong":
            case "redline":
            case "regattaGates":
            {
                int intensity = layer.GetProperty("intensity").GetInt32();
                int seed = layer.GetProperty("seed").GetInt32();
                List<RaceGate> gates = kind switch
                {
                    "switchback" => SwitchbackCourse.Generate(seed, Apply(SwitchbackCourseSettings.ForIntensity(intensity), layer)),
                    "headlong" => HeadlongCircuit.Generate(seed, Apply(HeadlongCircuitSettings.ForIntensity(intensity), layer)),
                    "redline" => HeadlongCircuit.Generate(seed, Apply(RedlineCourse.ForIntensity(intensity), layer)),
                    _ => RegattaCourse.BuildGates(seed, intensity),
                };
                if (gates == null || gates.Count == 0) { UnityEngine.Debug.LogError($"{kind}: course generator returned no gates"); return 0; }
                var gm = GateMaterial(layer);
                double tube = layer.TryGetProperty("tube", out var te) ? te.GetDouble() : 0.07;
                foreach (var g in gates)
                    scene.Ring(ToV(g.Position), ToV(g.Axis), g.Radius, g.Radius * tube, gm, 56, 8);
                if (layer.TryGetProperty("path", out var pe) && pe.GetBoolean())
                    DrawPath(scene, gates.Select(g => ToV(g.Position)).ToList(), kind != "switchback", pal, layer);
                return 0;
            }
            case "breakwater":
            {
                int intensity = layer.GetProperty("intensity").GetInt32();
                int seed = layer.GetProperty("seed").GetInt32();
                var s = Apply(BreakwaterCourseSettings.ForIntensity(intensity), layer);
                var stations = BreakwaterCourse.Generate(seed, s);
                if (stations == null || stations.Count == 0) { UnityEngine.Debug.LogError("breakwater: no stations"); return 0; }
                int n = 0;
                var gm = GateMaterial(layer);
                foreach (var st in stations)
                {
                    BreakwaterStationBuilder.Build(st, (pos, rot, scl, k) =>
                    {
                        AddPrism(scene, new SpawnPoint(pos, rot, scl), pal.For(Domains.Blue, k));
                        n++;
                    });
                    scene.Ring(ToV(st.Position), ToV(st.Axis), st.PortRadius, st.PortRadius * 0.05, gm, 48, 6);
                }
                return n;
            }
            case "box":
            {
                var c = Vec(layer, "c"); var ax = Vec(layer, "ax"); var ay = Vec(layer, "ay"); var az = Vec(layer, "az");
                scene.Box(c, ax, ay, az, MaterialFrom(layer, pal));
                return 1;
            }
            case "prisms":
            {
                // A modelled lay list: [x,y,z, qx,qy,qz,qw, sx,sy,sz, domain, kind] per row.
                int n = 0;
                foreach (var row in layer.GetProperty("rows").EnumerateArray())
                {
                    var a = row.EnumerateArray().Select(e => e.GetDouble()).ToArray();
                    var sp = new SpawnPoint(new Vector3((float)a[0], (float)a[1], (float)a[2]),
                        new Quaternion((float)a[3], (float)a[4], (float)a[5], (float)a[6]),
                        new Vector3((float)a[7], (float)a[8], (float)a[9]));
                    AddPrism(scene, sp, pal.For((Domains)(int)a[10], (PrismKind)(int)a[11]));
                    n++;
                }
                return n;
            }
            case "ring":
                scene.Ring(Vec(layer, "c"), Vec(layer, "n"), layer.GetProperty("R").GetDouble(), layer.GetProperty("r").GetDouble(),
                    MaterialFrom(layer, pal), 64, 10, !layer.TryGetProperty("unbound", out _));
                return 0;
            case "sphere":
                scene.Sphere(Vec(layer, "c"), layer.GetProperty("r").GetDouble(), MaterialFrom(layer, pal), 28, 56,
                    !layer.TryGetProperty("unbound", out _));
                return 0;
            case "cone":
                scene.Cone(Vec(layer, "apex"), Vec(layer, "axis"), layer.GetProperty("length").GetDouble(),
                    layer.GetProperty("halfAngle").GetDouble(), MaterialFrom(layer, pal));
                return 0;
            case "shell":
                scene.Shell(Vec(layer, "c"), layer.GetProperty("r").GetDouble(), Vec(layer, "color"), layer.GetProperty("strength").GetDouble());
                return 0;
            default:
                UnityEngine.Debug.LogError($"unknown layer kind '{kind}'");
                return 0;
        }
    }

    /// <summary>Course settings are value types built by ForIntensity; a spec may override the
    /// fields a CONTROLLER sets at runtime (a gate count that lives in EndConditionOverrides).</summary>
    static T Apply<T>(T settings, JsonElement layer)
    {
        if (!layer.TryGetProperty("settings", out var s)) return settings;
        object boxed = settings;
        foreach (var p in s.EnumerateObject())
        {
            var f = FindField(typeof(T), p.Name);
            if (f == null) { UnityEngine.Debug.LogError($"{typeof(T).Name} has no field '{p.Name}'"); continue; }
            f.SetValue(boxed, Convert(f.FieldType, p.Value, p.Name));
        }
        return (T)boxed;
    }

    /// <summary>
    /// STAGING: pilots in flight. A dart per vessel plus the prism ribbon it lays - the trail is
    /// conserved mass in the pilot's own domain, the one visual every mode shares, and a card with
    /// no pilot in it reads as a level select rather than a game. Paths are arcs around the
    /// FRAMED mass (so they pass through the shot rather than behind it) and never count toward
    /// the framing themselves.
    /// </summary>
    static void Stage(JsonElement s, Scene scene, Palette pal)
    {
        var rng = new System.Random(s.GetProperty("seed").GetInt32());
        var centre = new V3(0, 0, 0);
        foreach (var p in scene.Samples) centre = centre + p;
        if (scene.Samples.Count > 0) centre = centre * (1.0 / scene.Samples.Count);
        var dists = scene.Samples.Select(p => (p - centre).Len).OrderBy(x => x).ToList();
        double R = dists.Count > 0 ? dists[(int)(dists.Count * 0.7)] : 500;
        if (s.TryGetProperty("centre", out var ce)) centre = Vec(ce);
        if (s.TryGetProperty("radius", out var re)) R = re.GetDouble();
        double scale = s.TryGetProperty("scale", out var se) ? se.GetDouble() : 1.0;
        foreach (var v in s.GetProperty("vessels").EnumerateArray())
        {
            var dom = (Domains)v.GetProperty("domain").GetInt32();
            double radius = R * (v.TryGetProperty("r", out var rr) ? rr.GetDouble() : 0.5 + rng.NextDouble() * 0.4);
            double tilt = (v.TryGetProperty("tilt", out var tt) ? tt.GetDouble() : rng.NextDouble() * 70 - 35) * Math.PI / 180;
            double a0 = (v.TryGetProperty("a0", out var aa) ? aa.GetDouble() : rng.NextDouble() * 360) * Math.PI / 180;
            double sweep = (v.TryGetProperty("sweep", out var sw) ? sw.GetDouble() : 70) * Math.PI / 180;
            double yaw = (v.TryGetProperty("yaw", out var yw) ? yw.GetDouble() : rng.NextDouble() * 360) * Math.PI / 180;
            V3 P(double t)
            {
                double a = a0 + sweep * t;
                var p = new V3(Math.Cos(a) * radius, Math.Sin(a) * radius * Math.Sin(tilt), Math.Sin(a) * radius * Math.Cos(tilt));
                p = new V3(p.x * Math.Cos(yaw) - p.z * Math.Sin(yaw), p.y, p.x * Math.Sin(yaw) + p.z * Math.Cos(yaw));
                return centre + p;
            }
            double len = R * 0.028 * scale;
            var m = pal.For(dom, PrismKind.Plain);
            double arcLen = radius * sweep;
            int n = Math.Max(6, (int)(arcLen / (len * 1.3)));
            for (int i = 0; i < n; i++)
            {
                double t = (double)i / (n - 1);
                var p = P(t); var f = (P(Math.Min(1, t + 1e-3)) - P(Math.Max(0, t - 1e-3))).Norm;
                var side = V3.Cross(new V3(0, 1, 0), f).Norm; var up = V3.Cross(f, side);
                double w = len * (0.12 + 0.28 * t);
                scene.Box(p, side * w, up * (w * 0.5), f * (len * 0.5), m, false);
            }
            // the dart: a hull and two swept wings, super-shielded palette so it reads brightest
            var head = P(1); var fwd = (P(1) - P(0.995)).Norm;
            var sd = V3.Cross(new V3(0, 1, 0), fwd).Norm; var upv = V3.Cross(fwd, sd);
            double body = len * 2.6;
            var hull = pal.For(dom, PrismKind.SuperShielded);
            var bright = new Material { Base = hull.Base, Rim = hull.Rim, Emissive = hull.Rim * 0.35, EdgeGlow = 1.2 };
            var nose = head + fwd * (body * 1.1);
            scene.Box(nose, sd * (body * 0.2), upv * (body * 0.13), fwd * (body * 0.7), bright, false);
            for (int k = -1; k <= 1; k += 2)
            {
                var wf = (fwd * 0.6 + sd * (k * 0.8)).Norm; var wu = V3.Cross(wf, V3.Cross(upv, wf)).Norm;
                scene.Box(nose + sd * (k * body * 0.42) - fwd * (body * 0.35), V3.Cross(wu, wf).Norm * (body * 0.09), wu * (body * 0.04), wf * (body * 0.5), bright, false);
            }
        }
    }

    static void DrawPath(Scene scene, List<V3> pts, bool loop, Palette pal, JsonElement layer)
    {
        // A faint ribbon of small plates between gates: reads as "a course" at card size.
        var m = new Material { Base = new V3(0.05, 0.08, 0.2), Rim = new V3(0.5, 0.7, 1.4), Emissive = new V3(0.05, 0.08, 0.2), EdgeGlow = 0.2 };
        int count = loop ? pts.Count : pts.Count - 1;
        for (int i = 0; i < count; i++)
        {
            var a = pts[i]; var b = pts[(i + 1) % pts.Count];
            var d = b - a; double len = d.Len; var f = d.Norm;
            int steps = Math.Max(2, (int)(len / 60));
            var up = Math.Abs(f.y) < 0.9 ? new V3(0, 1, 0) : new V3(1, 0, 0);
            var r = V3.Cross(up, f).Norm; var u = V3.Cross(f, r);
            for (int s = 1; s < steps; s++)
            {
                var c = a + d * ((double)s / steps);
                scene.Box(c, f * 6, r * 1.2, u * 1.2, m, false);
            }
        }
    }

    static Material GateMaterial(JsonElement layer)
    {
        var c = layer.TryGetProperty("color", out _) ? Vec(layer, "color") : new V3(0.35, 0.45, 1.3);
        return new Material { Base = c * 0.25, Rim = c * 1.2, Emissive = c * 0.9, EdgeGlow = 0, RimPower = 1.5 };
    }

    static Material MaterialFrom(JsonElement layer, Palette pal)
    {
        if (layer.TryGetProperty("domain", out var de))
        {
            var k = layer.TryGetProperty("tier", out var te) ? (PrismKind)te.GetInt32() : PrismKind.Plain;
            var baseM = pal.For((Domains)de.GetInt32(), k);
            var m = new Material { Base = baseM.Base, Rim = baseM.Rim };
            if (layer.TryGetProperty("emissive", out _)) m.Emissive = Vec(layer, "emissive");
            if (layer.TryGetProperty("alpha", out var ae)) m.Alpha = ae.GetDouble();
            if (layer.TryGetProperty("edge", out var ee)) m.EdgeGlow = ee.GetDouble();
            return m;
        }
        var mm = new Material
        {
            Base = Vec(layer, "base"),
            Rim = layer.TryGetProperty("rim", out _) ? Vec(layer, "rim") : Vec(layer, "base"),
            Emissive = layer.TryGetProperty("emissive", out _) ? Vec(layer, "emissive") : new V3(0, 0, 0),
        };
        if (layer.TryGetProperty("alpha", out var a2)) mm.Alpha = a2.GetDouble();
        if (layer.TryGetProperty("edge", out var e2)) mm.EdgeGlow = e2.GetDouble();
        if (layer.TryGetProperty("rimPower", out var rp)) mm.RimPower = rp.GetDouble();
        return mm;
    }

    static void AddPrism(Scene scene, SpawnPoint p, Material m)
    {
        var q = p.Rotation;
        var ax = ToV(q * Vector3.right) * (p.Scale.x * 0.5);
        var ay = ToV(q * Vector3.up) * (p.Scale.y * 0.5);
        var az = ToV(q * Vector3.forward) * (p.Scale.z * 0.5);
        scene.Box(ToV(p.Position), ax, ay, az, m);
    }

    // ── generators ────────────────────────────────────────────────────────────────────────────

    /// <summary>A rigid TRS, applied the way a nested container's transform applies to its
    /// children (SpawnableBase.SpawnChildren).</summary>
    readonly struct Matrix
    {
        public readonly Vector3 T; public readonly Quaternion R; public readonly Vector3 S;
        public Matrix(Vector3 t, Quaternion r, Vector3 s) { T = t; R = r; S = s; }
        public static Matrix Identity => new(Vector3.zero, Quaternion.identity, Vector3.one);
        public Vector3 Point(Vector3 p) => T + R * Vector3.Scale(S, p);
        public Matrix Then(Vector3 t, Quaternion r, Vector3 s) => new(Point(t), R * r, Vector3.Scale(S, s));
    }

    static readonly Assembly Asm = typeof(Program).Assembly;

    static void RunGenerator(JsonElement node, Matrix parent, List<(SpawnPoint, Domains, PrismKind)> sink)
    {
        string typeName = node.GetProperty("type").GetString();
        var type = Asm.GetTypes().FirstOrDefault(t => t.Name == typeName || t.FullName == typeName);
        if (type == null) { UnityEngine.Debug.LogError($"generator type '{typeName}' is not compiled into the harness (add it to render_card_backgrounds.py's SOURCES)"); return; }
        var gen = (SpawnableBase)Activator.CreateInstance(type);
        if (node.TryGetProperty("fields", out var fields))
            foreach (var f in fields.EnumerateObject()) SetField(gen, f.Name, f.Value);

        // SpawnableBase.GetTrailData seeds rng before GenerateTrailData.
        var seedField = FindField(type, "seed");
        int seed = seedField != null ? (int)seedField.GetValue(gen) : 0;
        FindField(type, "rng").SetValue(gen, new System.Random(seed != 0 ? seed : 1));

        var local = parent;
        if (node.TryGetProperty("position", out _) || node.TryGetProperty("euler", out _) || node.TryGetProperty("scale", out _))
        {
            var t = node.TryGetProperty("position", out var pe) ? ToUV(pe) : Vector3.zero;
            var r = node.TryGetProperty("euler", out var ee) ? Quaternion.Euler(ToUV(ee)) : Quaternion.identity;
            var s = node.TryGetProperty("scale", out var se) ? ToUV(se) : Vector3.one;
            local = parent.Then(t, r, s);
        }

        // A generator that lays in Spawn() rather than GenerateTrailData exposes a PREVIEW API
        // instead (SpawnableWaypointTrack.GetPreviewBlocks - what the mode preview's track model
        // reads); use it, so the harness never reaches a lay path.
        var preview = type.GetMethod("GetPreviewBlocks", BindingFlags.Instance | BindingFlags.Public);
        if (preview != null)
        {
            int intensity = node.TryGetProperty("intensity", out var ie) ? ie.GetInt32() : 1;
            var domainOf = new Func<bool, Domains>(marker =>
                (Domains)FindField(type, marker ? "waypointDomain" : "trackDomain").GetValue(gen));
            foreach (var b in (IEnumerable)preview.Invoke(gen, new object[] { intensity }))
            {
                var bt = b.GetType();
                var sp = new SpawnPoint((Vector3)bt.GetField("Position").GetValue(b), (Quaternion)bt.GetField("Rotation").GetValue(b),
                    (Vector3)bt.GetField("Scale").GetValue(b));
                sink.Add((Xform(local, sp), domainOf((bool)bt.GetField("IsMarker").GetValue(b)), PrismKind.Plain));
            }
            return;
        }

        var trails = gen.GenerateForHarness();
        var children = node.TryGetProperty("children", out var ce) ? ce.EnumerateArray().ToList() : new List<JsonElement>();

        if (children.Count > 0)
        {
            // SpawnChildren: normalise point scales by the largest component when it exceeds 1.
            float maxComp = 0f;
            foreach (var td in trails) foreach (var p in td.Points)
                maxComp = Mathf.Max(maxComp, Mathf.Max(Mathf.Abs(p.Scale.x), Mathf.Max(Mathf.Abs(p.Scale.y), Mathf.Abs(p.Scale.z))));
            float norm = maxComp > 1f ? maxComp : 1f;
            foreach (var td in trails) foreach (var p in td.Points)
                foreach (var child in children)
                    RunGenerator(child, local.Then(p.Position, p.Rotation, p.Scale / norm), sink);
            return;
        }

        if (gen is CellEnvironmentSpawnableBase env && env.CachedLays != null)
        {
            foreach (var lay in env.CachedLays) sink.Add((Xform(local, lay.Point), lay.Domain, lay.Kind));
            return;
        }
        foreach (var td in trails)
            foreach (var p in td.Points)
                sink.Add((Xform(local, p), td.Domain, PrismKind.Plain));
    }

    static SpawnPoint Xform(Matrix m, SpawnPoint p) =>
        new SpawnPoint(m.Point(p.Position), m.R * p.Rotation, Vector3.Scale(m.S, p.Scale));

    static FieldInfo FindField(Type t, string name)
    {
        for (var c = t; c != null; c = c.BaseType)
        {
            var f = c.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (f != null) return f;
        }
        return null;
    }

    static void SetField(object target, string name, JsonElement v)
    {
        var f = FindField(target.GetType(), name);
        if (f == null) { Console.Error.WriteLine($"[cardart] note: {target.GetType().Name} has no field '{name}' (retired key?) - skipped"); return; }
        f.SetValue(target, Convert(f.FieldType, v, name));
    }

    static object Convert(Type t, JsonElement v, string name)
    {
        if (v.ValueKind == JsonValueKind.String)
        {
            // Raw YAML scalar token (see render_card_backgrounds.fields): parse by the field's type.
            string s = v.GetString();
            if (t.IsArray) return HexArray(t.GetElementType(), s, name);
            if (t == typeof(int)) return int.Parse(s, CultureInfo.InvariantCulture);
            if (t == typeof(float)) return float.Parse(s, CultureInfo.InvariantCulture);
            if (t == typeof(double)) return double.Parse(s, CultureInfo.InvariantCulture);
            if (t == typeof(bool)) return s != "0";
            if (t.IsEnum) return Enum.ToObject(t, long.Parse(s, CultureInfo.InvariantCulture));
            if (t == typeof(string)) return s;
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>))
            {
                var arr = (Array)HexArray(t.GetGenericArguments()[0], s, name);
                var list = (IList)Activator.CreateInstance(t);
                foreach (var o in arr) list.Add(o);
                return list;
            }
        }
        if (t == typeof(int)) return v.GetInt32();
        if (t == typeof(float)) return (float)v.GetDouble();
        if (t == typeof(double)) return v.GetDouble();
        if (t == typeof(bool)) return v.ValueKind == JsonValueKind.True || (v.ValueKind == JsonValueKind.Number && v.GetInt32() != 0);
        if (t == typeof(string)) return v.GetString();
        if (t.IsEnum) return Enum.ToObject(t, v.GetInt32());
        if (t == typeof(Vector3)) return ToUV(v);
        if (t == typeof(Vector2)) { var a = v.EnumerateArray().Select(e => (float)e.GetDouble()).ToArray(); return new Vector2(a[0], a[1]); }
        if (t.IsArray && t.GetElementType() != null)
        {
            var et = t.GetElementType();
            var items = v.EnumerateArray().Select(e => Convert(et, e, name)).ToArray();
            var arr = Array.CreateInstance(et, items.Length);
            for (int i = 0; i < items.Length; i++) arr.SetValue(items[i], i);
            return arr;
        }
        if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>))
        {
            var et = t.GetGenericArguments()[0];
            var list = (IList)Activator.CreateInstance(t);
            foreach (var e in v.EnumerateArray()) list.Add(Convert(et, e, name));
            return list;
        }
        if (v.ValueKind == JsonValueKind.Object && !t.IsPrimitive)
        {
            var o = Activator.CreateInstance(t);
            foreach (var p in v.EnumerateObject())
            {
                var f = FindField(t, p.Name);
                if (f != null) f.SetValue(o, Convert(f.FieldType, p.Value, p.Name));
            }
            return o;
        }
        throw new InvalidOperationException($"field '{name}': cannot convert JSON {v.ValueKind} to {t.Name}");
    }

    /// <summary>Unity serializes a primitive array as ONE little-endian hex blob.</summary>
    static object HexArray(Type et, string hex, string name)
    {
        int size = et == typeof(bool) || et == typeof(byte) ? 1 : et == typeof(double) || et == typeof(long) ? 8 : 4;
        if (hex.Length % (size * 2) != 0) throw new InvalidOperationException($"field '{name}': hex blob length {hex.Length} is not a whole number of {et.Name}s");
        int n = hex.Length / (size * 2);
        var bytes = System.Convert.FromHexString(hex);
        var arr = Array.CreateInstance(et, n);
        for (int i = 0; i < n; i++)
        {
            object o;
            if (et == typeof(bool)) o = bytes[i] != 0;
            else if (et == typeof(float)) o = BitConverter.ToSingle(bytes, i * 4);
            else if (et == typeof(int)) o = BitConverter.ToInt32(bytes, i * 4);
            else if (et.IsEnum) o = Enum.ToObject(et, BitConverter.ToInt32(bytes, i * 4));
            else throw new InvalidOperationException($"field '{name}': no hex decoding for {et.Name}");
            arr.SetValue(o, i);
        }
        return arr;
    }

    // ── camera, look, palette ─────────────────────────────────────────────────────────────────

    static CardArt.Camera BuildCamera(JsonElement c, Scene scene, double aspect)
    {
        double fov = c.TryGetProperty("fov", out var fe) ? fe.GetDouble() : 40;
        // Frame the MASS, not the bounds: centroid of the samples, radius at a high percentile of
        // their distance from it, so one outlier cannot shrink the arena to a speck.
        var centre = (scene.BoundsMin + scene.BoundsMax) * 0.5;
        double sampleRadius = (scene.BoundsMax - scene.BoundsMin).Len * 0.5;
        if (scene.Samples.Count > 0)
        {
            var sum = new V3(0, 0, 0);
            foreach (var p in scene.Samples) sum = sum + p;
            centre = sum * (1.0 / scene.Samples.Count);
            var d = scene.Samples.Select(p => (p - centre).Len).OrderBy(x => x).ToList();
            double pct = c.TryGetProperty("percentile", out var pe2) ? pe2.GetDouble() : 0.9;
            sampleRadius = d[Math.Min(d.Count - 1, (int)(d.Count * pct))];
        }
        if (c.TryGetProperty("centreOrigin", out var co) && co.GetBoolean()) centre = new V3(0, 0, 0);
        if (c.TryGetProperty("target", out var te)) centre = Vec(te);
        if (c.TryGetProperty("pos", out var pe))
            return new CardArt.Camera(Vec(pe), centre, new V3(0, 1, 0), fov, aspect);
        double radius = c.TryGetProperty("radius", out var re) ? re.GetDouble() : sampleRadius;
        double az = c.GetProperty("azimuth").GetDouble() * Math.PI / 180, el = c.GetProperty("elevation").GetDouble() * Math.PI / 180;
        double k = c.TryGetProperty("distance", out var de) ? de.GetDouble() : 1.0;
        // Distance at which a sphere of `radius` exactly fills the vertical field, times k.
        double dist = radius / Math.Sin(fov * Math.PI / 360) * k;
        var dir = new V3(Math.Cos(el) * Math.Sin(az), Math.Sin(el), Math.Cos(el) * Math.Cos(az));
        return new CardArt.Camera(centre + dir * dist, centre, new V3(0, 1, 0), fov, aspect);
    }

    static Look ReadLook(JsonElement l)
    {
        var look = new Look();
        if (l.ValueKind != JsonValueKind.Object) return look;
        foreach (var p in l.EnumerateObject())
        {
            var f = typeof(Look).GetField(p.Name);
            if (f == null) { UnityEngine.Debug.LogError($"look: unknown field '{p.Name}'"); continue; }
            if (f.FieldType == typeof(V3)) f.SetValue(look, Vec(p.Value));
            else if (f.FieldType == typeof(int)) f.SetValue(look, p.Value.GetInt32());
            else f.SetValue(look, p.Value.GetDouble());
        }
        look.LightDir = look.LightDir.Norm;
        return look;
    }

    static Palette ReadPalette(JsonElement p)
    {
        var pal = new Palette { Danger = Vec(p.GetProperty("danger")) };
        foreach (var d in p.GetProperty("domains").EnumerateObject())
        {
            int dom = int.Parse(d.Name, CultureInfo.InvariantCulture);
            var t = new (V3, V3)[3];
            int i = 0;
            foreach (var tier in new[] { "plain", "shielded", "super" })
            {
                var pair = d.Value.GetProperty(tier);
                t[i++] = (Vec(pair[0]), Vec(pair[1]));
            }
            pal.Tiers[dom] = t;
        }
        return pal;
    }

    static V3 ToV(Vector3 v) => new V3(v.x, v.y, v.z);
    static Vector3 ToUV(JsonElement e) { var a = e.EnumerateArray().Select(x => (float)x.GetDouble()).ToArray(); return new Vector3(a[0], a[1], a[2]); }
    static V3 Vec(JsonElement e) { var a = e.EnumerateArray().Select(x => x.GetDouble()).ToArray(); return new V3(a[0], a[1], a[2]); }
    static V3 Vec(JsonElement o, string name) => Vec(o.GetProperty(name));
}
