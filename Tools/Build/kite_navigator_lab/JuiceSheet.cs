// JuiceSheet.cs - renders the SHIPPED KiteNavigatorJuice (compiled from its real path with the card-art harness's Unity
// shim) as a contact sheet: one row per effect, one column per moment, the kite view seen from above. Numbers come from
// KiteNavigatorConfig.asset and KiteNavigatorView.cs, the petal shapes from ElementPetalGeometry.cs. Fails (exit 1) when
// a vertex is not finite or an effect draws nothing while it should be on screen. Run by juice_sheet.sh.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.UI;
using UnityEngine;

public static class JuiceSheet
{
    const int Panel = 320, Gap = 6;
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    static int _faults;

    sealed class Scene
    {
        public string Name;
        public float CentreX, CentreZ, Half;
        public float[] Times;
        public Func<float, Vector3> Kite = _ => new Vector3(60f, 0f, 60f);
        public List<KiteBurst> Bursts = new();
    }

    public static int Main(string[] args)
    {
        string root = args.Length > 0 ? args[0] : ".";
        string outPath = args.Length > 1 ? args[1] : "juice_sheet.png";
        bool pilotView = args.Length > 2 && args[2] == "pilot";   // the pilot's chase camera, in perspective
        var asset = File.ReadAllText(Path.Combine(root, "Assets/_SO_Assets/VesselActions/KiteNavigator/KiteNavigatorConfig.asset"));
        var view = File.ReadAllText(Path.Combine(root, "Assets/_Scripts/Controller/Vessel/Crew/KiteNavigatorView.cs"));

        var outlines = new Vector3[4][];
        for (int e = 0; e < 4; e++)
        {
            var o = ElementPetalGeometry.Outline((Element)(e + 1));
            outlines[e] = new Vector3[o.Length];
            for (int k = 0; k < o.Length; k++) outlines[e][k] = new Vector3((o[k].x - 0.5f) * 2f, 0f, (o[k].y - 0.5f) * 2f);
        }
        var settings = new KiteJuiceSettings
        {
            MoteSize = F(asset, "moteSize"), SpaceSpinDegreesPerSecond = F(asset, "spaceSpinDegreesPerSecond"),
            KiteRadius = F(asset, "kiteRadius"), MoteSpread = F(asset, "moteSpread"),
            FizzleSeconds = F(asset, "fizzleSeconds"), SpaceRideSeconds = F(asset, "spaceRideSeconds"),
            MassMorphSeconds = F(asset, "massMorphSeconds"), TimeFlightSeconds = F(asset, "timeFlightSeconds"),
            ChargeFlightSeconds = F(asset, "chargeFlightSeconds"), MassPrismScale = V3(asset, "massPrismScale"),
            PetalCount = 5, PetalSpacing = 72f, Elements = Colours(asset),
        };
        float viewDepth = F(asset, "viewDepth");
        var petalOut = Floats(view, "PetalCentreOut");
        var petalScale = Floats(view, "PetalScale");

        var scenes = Scenes(settings);
        int cols = 0;
        foreach (var s in scenes) cols = Math.Max(cols, s.Times.Length);
        int w = cols * Panel + (cols + 1) * Gap, h = scenes.Count * Panel + (scenes.Count + 1) * Gap;
        var img = new float[w * h * 3];
        Fill(img, w, 0, 0, w, h, 0.03f, 0.04f, 0.06f);

        for (int r = 0; r < scenes.Count; r++)
        {
            var s = scenes[r];
            var juice = new KiteNavigatorJuice(outlines, petalOut, petalScale);
            var frame0 = Frame(s, 0f, viewDepth);
            var pending = new List<KiteBurst>(s.Bursts);
            juice.Drain(pending, _ => { }, frame0, settings);
            for (int c = 0; c < s.Times.Length; c++)
            {
                float t = s.Times[c];
                var frame = Frame(s, t, viewDepth);
                juice.Build(frame, settings);
                int ox = Gap + c * (Panel + Gap), oy = Gap + r * (Panel + Gap);
                Fill(img, w, ox, oy, Panel, Panel, 0.012f, 0.03f, 0.04f);
                int tris;
                if (pilotView)
                    tris = Perspective(img, w, ox, oy, frame, juice.Vertices(1), juice.Colors(1), juice.Triangles(1));
                else
                {
                    Context(img, w, ox, oy, s, frame);
                    tris = Raster(img, w, ox, oy, s, juice.Vertices(0), juice.Colors(0), juice.Triangles(0));
                }
                CheckFinite(juice.Vertices(1), s.Name, t);
                if (tris == 0 && c < s.Times.Length - 1)
                {
                    Console.Error.WriteLine($"FAIL: {s.Name} draws nothing at {t:0.00} s");
                    _faults++;
                }
                Console.WriteLine($"{s.Name,-22} t={t:0.00}s  kite view {tris,5} tris, pilot view {juice.Triangles(1).Count / 3,5} tris, {juice.Count} bits");
            }
        }
        WritePng(outPath, img, w, h);
        Console.WriteLine($"wrote {outPath} ({w}x{h}); rows: " + string.Join(", ", scenes.ConvertAll(s => s.Name)));
        return _faults == 0 ? 0 : 1;
    }

    // ------------------------------------------------------------------ the scenes (board coordinates: x right, z forward)

    static List<Scene> Scenes(KiteJuiceSettings s)
    {
        var list = new List<Scene>();
        KiteBurst B(KiteBurstKind kind, float x, float z, params (float x, float z, int e)[] motes)
        {
            var b = new KiteBurst { Kind = kind, X = x, Z = z, T0 = 0f, World = new Vector3(x, 0f, z) };
            int k = 0;
            foreach (var m in motes) b.Motes.Add(new KiteBurstMote { X = m.x, Z = m.z, Spin = 37f * k++, Element = m.e });
            return b;
        }

        list.Add(new Scene
        {
            Name = "Fizzle (Mass+Time+Space)", CentreX = -6f, CentreZ = 4f, Half = 34f,
            Times = new[] { 0f, 0.1f, 0.25f, 0.45f, 0.65f, 0.85f },
            Bursts = { B(KiteBurstKind.Fizzle, -6f, 6f, (-14f, 10f, 1), (-4f, 16f, 3), (4f, 6f, 2), (-10f, -1f, 1), (0f, 0f, 3)) },
        });

        var space = B(KiteBurstKind.Space, 0f, 0f, (12f, 4f, 2), (-8f, 10f, 2), (-4f, -12f, 2));
        space.MaxScale = 240f;   // three motes: 60 x 2^2
        list.Add(new Scene { Name = "Space (3 motes)", CentreX = 0f, CentreZ = 0f, Half = 62f,
                             Times = new[] { 0f, 0.1f, 0.2f, 0.35f, 0.65f, 1.05f }, Bursts = { space } });

        var mass = B(KiteBurstKind.Mass, 0f, 0f, (9f, 3f, 1), (-7f, 7f, 1), (-2f, -9f, 1));
        var rng = new System.Random(5);
        float rad = Math.Max(4f, 2.6f * MathF.Pow(24f, 1f / 3f));
        for (int i = 0; i < 24; i++)
        {
            var dir = new Vector3((float)rng.NextDouble() * 2f - 1f, (float)rng.NextDouble() * 2f - 1f, (float)rng.NextDouble() * 2f - 1f).normalized;
            float d = rad * MathF.Pow((float)rng.NextDouble(), 1f / 3f);
            mass.Targets.Add((dir * d, Quaternion.Euler((float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 360f, 0f)));
        }
        list.Add(new Scene { Name = "Mass (3 motes, 24 prisms)", CentreX = 0f, CentreZ = 0f, Half = 26f,
                             Times = new[] { 0f, 0.12f, 0.3f, 0.5f, 0.78f, 0.98f }, Bursts = { mass } });

        list.Add(new Scene
        {
            Name = "Time (3 motes -> Grouper)", CentreX = -20f, CentreZ = 22f, Half = 42f,
            Times = new[] { 0f, 0.2f, 0.4f, 0.6f, 0.85f, 1.15f },
            Bursts = { B(KiteBurstKind.Time, -42f, 46f, (-48f, 44f, 3), (-38f, 52f, 3), (-40f, 40f, 3)) },
        });

        list.Add(new Scene
        {
            Name = "Charge (3 motes -> Wrasse)", CentreX = 4f, CentreZ = 8f, Half = 44f,
            Times = new[] { 0f, 0.15f, 0.35f, 0.55f, 0.8f, 1.0f },
            Kite = t => new Vector3(28f + 18f * t, 0f, 24f - 6f * t),
            Bursts = { B(KiteBurstKind.Charge, -26f, -10f, (-30f, -8f, 0), (-22f, -14f, 0), (-26f, -4f, 0)) },
        });

        var pop = B(KiteBurstKind.Pop, -28f, 6f); pop.World = new Vector3(-28f, 0f, 6f);
        var steal = B(KiteBurstKind.Steal, 0f, 30f); steal.World = new Vector3(0f, 0f, 30f);
        var bloom = B(KiteBurstKind.Bloom, -8f, -28f); bloom.Element = 2;
        list.Add(new Scene
        {
            Name = "Pop, Steal, Omni bloom", CentreX = 0f, CentreZ = 0f, Half = 46f,
            Times = new[] { 0f, 0.08f, 0.18f, 0.3f, 0.45f, 0.6f },
            Kite = _ => new Vector3(30f, 0f, -12f),
            Bursts = { pop, steal, bloom },
        });
        return list;
    }

    static KiteJuiceFrame Frame(Scene s, float t, float viewDepth) => new()
    {
        Now = t, ViewDepth = viewDepth,
        Origin = Vector3.zero, Up = Vector3.up, Right = Vector3.right, Forward = Vector3.forward,
        Kite = s.Kite(t),
        HasHull = true, HullPosition = Vector3.zero, HullRotation = Quaternion.identity,
        HasPilot = true, PilotPosition = new Vector3(0f, 22f, -60f), PilotUp = Vector3.up,
    };

    // ------------------------------------------------------------------ drawing

    static void Context(float[] img, int w, int ox, int oy, Scene s, KiteJuiceFrame f)
    {
        // the Grouper (a dark hull at the origin, nose up the sheet) and the Wrasse (a white diamond), for the Time and
        // Charge rows' destinations
        Box(img, w, ox, oy, s, -1.8f, -6f, 1.8f, 6f, 0.25f, 0.28f, 0.33f);
        Box(img, w, ox, oy, s, -6f, -1f, 6f, 1.6f, 0.2f, 0.22f, 0.27f);
        var k = f.Kite;
        Tri(img, w, ox, oy, s, new Vector3(k.x, 0, k.z + 3.2f), new Vector3(k.x + 2f, 0, k.z), new Vector3(k.x, 0, k.z - 2.6f), new Color(0.9f, 0.92f, 0.95f, 0.85f));
        Tri(img, w, ox, oy, s, new Vector3(k.x, 0, k.z + 3.2f), new Vector3(k.x, 0, k.z - 2.6f), new Vector3(k.x - 2f, 0, k.z), new Color(0.9f, 0.92f, 0.95f, 0.85f));
    }

    static void Box(float[] img, int w, int ox, int oy, Scene s, float x0, float z0, float x1, float z1, float r, float g, float b)
    {
        var c = new Color(r, g, b, 1f);
        Tri(img, w, ox, oy, s, new Vector3(x0, 0, z0), new Vector3(x1, 0, z0), new Vector3(x1, 0, z1), c);
        Tri(img, w, ox, oy, s, new Vector3(x0, 0, z0), new Vector3(x1, 0, z1), new Vector3(x0, 0, z1), c);
    }

    static int Raster(float[] img, int w, int ox, int oy, Scene s, List<Vector3> v, List<Color> c, List<int> t)
    {
        CheckFinite(v, s.Name, -1f);
        for (int i = 0; i + 2 < t.Count; i += 3)
        {
            var a = c[t[i]];
            Tri(img, w, ox, oy, s, v[t[i]], v[t[i + 1]], v[t[i + 2]], a);
        }
        return t.Count / 3;
    }

    // The pilot's view: a 60-degree perspective camera at the frame's pilot position looking at the hull, the hull drawn as a
    // dark block, the juice's triangles painted far to near.
    static readonly List<(float depth, int i)> _order = new();
    static int Perspective(float[] img, int w, int ox, int oy, KiteJuiceFrame f, List<Vector3> v, List<Color> c, List<int> t)
    {
        var eye = f.PilotPosition;
        var fwd = (f.HullPosition - eye).normalized;
        var right = Vector3.Cross(f.PilotUp, fwd).normalized;
        var up = Vector3.Cross(fwd, right);
        float focal = Panel * 0.5f / MathF.Tan(30f * MathF.PI / 180f);
        bool P(Vector3 p, out float x, out float y, out float z)
        {
            var d = p - eye; z = Vector3.Dot(d, fwd);
            x = Panel * 0.5f + Vector3.Dot(d, right) / Math.Max(z, 0.1f) * focal;
            y = Panel * 0.5f - Vector3.Dot(d, up) / Math.Max(z, 0.1f) * focal;
            return z > 0.5f;
        }
        void T(Vector3 a, Vector3 b, Vector3 cc, Color col)
        {
            if (!P(a, out float ax, out float ay, out _) || !P(b, out float bx, out float by, out _) || !P(cc, out float cx, out float cy, out _)) return;
            TriPx(img, w, ox, oy, ax, ay, bx, by, cx, cy, col);
        }
        // the Grouper as a dark block, nose away from the camera
        var hull = new Color(0.22f, 0.25f, 0.3f, 1f);
        Vector3 h0 = new(-1.8f, -1.2f, -6f), h1 = new(1.8f, 1.6f, 6f);
        T(new Vector3(h0.x, h1.y, h0.z), new Vector3(h1.x, h1.y, h0.z), new Vector3(h1.x, h1.y, h1.z), hull);
        T(new Vector3(h0.x, h1.y, h0.z), new Vector3(h1.x, h1.y, h1.z), new Vector3(h0.x, h1.y, h1.z), hull);
        T(new Vector3(h0.x, h0.y, h0.z), new Vector3(h1.x, h0.y, h0.z), new Vector3(h1.x, h1.y, h0.z), new Color(0.17f, 0.19f, 0.23f, 1f));
        T(new Vector3(h0.x, h0.y, h0.z), new Vector3(h1.x, h1.y, h0.z), new Vector3(h0.x, h1.y, h0.z), new Color(0.17f, 0.19f, 0.23f, 1f));
        CheckFinite(v, "pilot view", -1f);
        _order.Clear();
        for (int i = 0; i + 2 < t.Count; i += 3)
            _order.Add((Vector3.Dot((v[t[i]] + v[t[i + 1]] + v[t[i + 2]]) / 3f - eye, fwd), i));
        _order.Sort((a, b) => b.depth.CompareTo(a.depth));
        foreach (var (_, i) in _order) T(v[t[i]], v[t[i + 1]], v[t[i + 2]], c[t[i]]);
        return t.Count / 3;
    }

    static void CheckFinite(List<Vector3> v, string name, float t)
    {
        foreach (var p in v)
            if (!float.IsFinite(p.x) || !float.IsFinite(p.y) || !float.IsFinite(p.z))
            {
                Console.Error.WriteLine($"FAIL: {name} has a non-finite vertex at {t:0.00} s");
                _faults++;
                return;
            }
    }

    // a triangle seen from above (x right, z up the panel), alpha-blended
    static void Tri(float[] img, int w, int ox, int oy, Scene s, Vector3 a, Vector3 b, Vector3 c, Color col)
    {
        float scale = Panel / (2f * s.Half);
        float ax = (a.x - s.CentreX + s.Half) * scale, ay = (s.Half - (a.z - s.CentreZ)) * scale;
        float bx = (b.x - s.CentreX + s.Half) * scale, by = (s.Half - (b.z - s.CentreZ)) * scale;
        float cx = (c.x - s.CentreX + s.Half) * scale, cy = (s.Half - (c.z - s.CentreZ)) * scale;
        TriPx(img, w, ox, oy, ax, ay, bx, by, cx, cy, col);
    }

    static void TriPx(float[] img, int w, int ox, int oy, float ax, float ay, float bx, float by, float cx, float cy, Color col)
    {
        float area = (bx - ax) * (cy - ay) - (cx - ax) * (by - ay);
        if (MathF.Abs(area) < 1e-6f) return;
        int x0 = Math.Max(0, (int)MathF.Floor(MathF.Min(ax, MathF.Min(bx, cx)))), x1 = Math.Min(Panel - 1, (int)MathF.Ceiling(MathF.Max(ax, MathF.Max(bx, cx))));
        int y0 = Math.Max(0, (int)MathF.Floor(MathF.Min(ay, MathF.Min(by, cy)))), y1 = Math.Min(Panel - 1, (int)MathF.Ceiling(MathF.Max(ay, MathF.Max(by, cy))));
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                float px = x + 0.5f, py = y + 0.5f;
                float w0 = ((bx - px) * (cy - py) - (cx - px) * (by - py)) / area;
                float w1 = ((cx - px) * (ay - py) - (ax - px) * (cy - py)) / area;
                float w2 = 1f - w0 - w1;
                if (w0 < 0f || w1 < 0f || w2 < 0f) continue;
                int i = ((oy + y) * w + ox + x) * 3;
                float al = Math.Clamp(col.a, 0f, 1f);
                img[i] += (col.r - img[i]) * al; img[i + 1] += (col.g - img[i + 1]) * al; img[i + 2] += (col.b - img[i + 2]) * al;
            }
    }

    static void Fill(float[] img, int w, int ox, int oy, int pw, int ph, float r, float g, float b)
    {
        for (int y = oy; y < oy + ph; y++)
            for (int x = ox; x < ox + pw; x++) { int i = (y * w + x) * 3; img[i] = r; img[i + 1] = g; img[i + 2] = b; }
    }

    // ------------------------------------------------------------------ inputs

    static float F(string text, string key)
    {
        var m = Regex.Match(text, @"^\s*" + key + @": (-?[\d.]+)\s*$", RegexOptions.Multiline);
        if (!m.Success) throw new Exception("no " + key + " in the asset");
        return float.Parse(m.Groups[1].Value, Inv);
    }

    static Vector3 V3(string text, string key)
    {
        var m = Regex.Match(text, @"^\s*" + key + @": \{x: (-?[\d.]+), y: (-?[\d.]+), z: (-?[\d.]+)\}", RegexOptions.Multiline);
        if (!m.Success) throw new Exception("no " + key + " in the asset");
        return new Vector3(float.Parse(m.Groups[1].Value, Inv), float.Parse(m.Groups[2].Value, Inv), float.Parse(m.Groups[3].Value, Inv));
    }

    static Color[] Colours(string text)
    {
        var block = Regex.Match(text, @"elementColors:\n((?:  - \{[^\n]*\}\n){4})").Groups[1].Value;
        var ms = Regex.Matches(block, @"r: (-?[\d.]+), g: (-?[\d.]+), b: (-?[\d.]+)");
        if (ms.Count != 4) throw new Exception("elementColors not found");
        var c = new Color[4];
        for (int i = 0; i < 4; i++)
            c[i] = new Color(float.Parse(ms[i].Groups[1].Value, Inv), float.Parse(ms[i].Groups[2].Value, Inv), float.Parse(ms[i].Groups[3].Value, Inv), 1f);
        return c;
    }

    static float[] Floats(string text, string name)
    {
        var m = Regex.Match(text, name + @" = \{([^}]*)\}");
        if (!m.Success) throw new Exception("no " + name + " in KiteNavigatorView.cs");
        var parts = m.Groups[1].Value.Split(',');
        var f = new float[parts.Length];
        for (int i = 0; i < parts.Length; i++) f[i] = float.Parse(parts[i].Trim().TrimEnd('f'), Inv);
        return f;
    }

    // ------------------------------------------------------------------ PNG

    static void WritePng(string path, float[] img, int w, int h)
    {
        var raw = new byte[h * (w * 3 + 1)];
        for (int y = 0; y < h; y++)
        {
            raw[y * (w * 3 + 1)] = 0;
            for (int x = 0; x < w * 3; x++)
                raw[y * (w * 3 + 1) + 1 + x] = (byte)Math.Clamp((int)MathF.Round(img[y * w * 3 + x] * 255f), 0, 255);
        }
        using var fs = File.Create(path);
        fs.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        var hdr = new byte[13];
        BE(hdr, 0, w); BE(hdr, 4, h); hdr[8] = 8; hdr[9] = 2;
        Chunk(fs, "IHDR", hdr);
        using (var ms = new MemoryStream())
        {
            using (var z = new ZLibStream(ms, CompressionLevel.Optimal, true)) z.Write(raw);
            Chunk(fs, "IDAT", ms.ToArray());
        }
        Chunk(fs, "IEND", Array.Empty<byte>());
    }

    static void BE(byte[] b, int at, int v) { b[at] = (byte)(v >> 24); b[at + 1] = (byte)(v >> 16); b[at + 2] = (byte)(v >> 8); b[at + 3] = (byte)v; }

    static void Chunk(Stream s, string type, byte[] data)
    {
        var len = new byte[4]; BE(len, 0, data.Length); s.Write(len);
        var t = System.Text.Encoding.ASCII.GetBytes(type);
        s.Write(t); s.Write(data);
        uint crc = Crc(Crc(0xFFFFFFFFu, t), data) ^ 0xFFFFFFFFu;
        var cb = new byte[4]; BE(cb, 0, (int)crc); s.Write(cb);
    }

    static uint[] _table;
    static uint Crc(uint crc, byte[] data)
    {
        if (_table == null)
        {
            _table = new uint[256];
            for (uint n = 0; n < 256; n++) { uint c = n; for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1; _table[n] = c; }
        }
        foreach (var b in data) crc = _table[(crc ^ b) & 0xFF] ^ (crc >> 8);
        return crc;
    }
}
