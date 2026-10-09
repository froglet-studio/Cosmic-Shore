#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Prisma.Parity
{
    /// <summary>
    /// The per-channel tolerances of docs/ARCHITECTURE_REVIEW_2026-10-06.md (C9). Loaded from
    /// Port/parity/tolerances.json so gate G1 can calibrate them without a code change.
    /// </summary>
    public sealed class Tolerances
    {
        /// <summary>Event timestamps may differ by one fixed step.</summary>
        public double EventTimeSeconds { get; set; } = 0.04;
        /// <summary>Position error allowed per metre of distance from the origin (1 mm per 10 m).</summary>
        public double PositionRelative { get; set; } = 1e-4;
        /// <summary>Distance floor for the relative bar, so a body at the origin is not held to zero.</summary>
        public double PositionFloorMetres { get; set; } = 1.0;
        public double RotationDegrees { get; set; } = 0.1;
        /// <summary>Transforms are compared as paths only this long; after it, outcomes (state) only.</summary>
        public double TransformWindowSeconds { get; set; } = 10;
        public double SsimGameplay { get; set; } = 0.97;
        public double SsimUi { get; set; } = 0.98;
        /// <summary>UI rects (ui/*.jsonl): each corner within this many screen pixels.</summary>
        public double UiRectPixels { get; set; } = 1.0;
        /// <summary>UI text: the (auto-)sized font within this many points.</summary>
        public double UiFontSize { get; set; } = 0.5;

        public static Tolerances Load(string? path)
        {
            if (path == null || !File.Exists(path)) return new Tolerances();
            return JsonSerializer.Deserialize<Tolerances>(File.ReadAllText(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip }) ?? new Tolerances();
        }
    }

    public enum ChannelStatus { Pass, Fail, Missing }

    /// <summary>One channel's verdict. Missing: no golden (or no run output) - no claim either way.</summary>
    public sealed record ChannelResult(string Channel, ChannelStatus Status, string Detail)
    {
        public override string ToString() => $"{Channel,-10} {Status.ToString().ToUpperInvariant(),-7} {Detail}";
    }

    /// <summary>
    /// Diffs a parity run directory against a golden directory (same layout, Port/parity/README.md):
    /// state.jsonl exact, random_*.json exact, events.jsonl (fmod, game, contact) exact order/count
    /// with timestamps within a step, transforms.jsonl within the C9 bars during the first 10 s, frames/*.png by SSIM.
    /// </summary>
    public static class ParityDiff
    {
        public static List<ChannelResult> Compare(string golden, string actual, Tolerances tol)
        {
            return new List<ChannelResult>
            {
                State(golden, actual),
                Random(golden, actual),
                Events(golden, actual, tol),
                Transforms(golden, actual, tol),
                Frames(golden, actual, tol),
                Ui(golden, actual, tol),
            };
        }

        static List<JsonObject>? Lines(string path)
        {
            if (!File.Exists(path)) return null;
            return File.ReadAllLines(path).Where(l => l.Trim().Length > 0).Select(l => JsonNode.Parse(l)!.AsObject()).ToList();
        }

        static ChannelResult? MissingEither<T>(string channel, T? g, T? a) where T : class
            => g == null ? new ChannelResult(channel, ChannelStatus.Missing, "no golden")
             : a == null ? new ChannelResult(channel, ChannelStatus.Missing, "no run output")
             : null;

        // ---- gameplay state: exact, checkpoint by checkpoint ---------------------------------------

        public static ChannelResult State(string golden, string actual)
        {
            var g = Lines(Path.Combine(golden, "state.jsonl"));
            var a = Lines(Path.Combine(actual, "state.jsonl"));
            if (MissingEither("state", g, a) is { } m) return m;
            int n = Math.Min(g!.Count, a!.Count);
            for (int i = 0; i < n; i++)
            {
                var gs = Strip(g[i], "t"); var as_ = Strip(a[i], "t");
                if (!JsonNode.DeepEquals(gs, as_))
                    return new ChannelResult("state", ChannelStatus.Fail, $"checkpoint {i} (frame {g[i]["frame"]}): golden {gs.ToJsonString()} vs {as_.ToJsonString()}");
            }
            if (g.Count != a.Count) return new ChannelResult("state", ChannelStatus.Fail, $"{g.Count} golden checkpoints vs {a.Count}");
            return new ChannelResult("state", ChannelStatus.Pass, $"{n} checkpoints identical");
        }

        /// <summary>Game time is not state: it is quantised by the step, and the event channel bounds it.</summary>
        static JsonObject Strip(JsonObject o, params string[] keys)
        {
            var c = o.DeepClone().AsObject();
            foreach (var k in keys) c.Remove(k);
            return c;
        }

        // ---- Random: exact per seed ---------------------------------------------------------------

        public static ChannelResult Random(string golden, string actual)
        {
            var files = Directory.Exists(golden) ? Directory.GetFiles(golden, "random_*.json") : Array.Empty<string>();
            if (files.Length == 0) return new ChannelResult("random", ChannelStatus.Missing, "no golden");
            int draws = 0;
            foreach (var gf in files.OrderBy(f => f, StringComparer.Ordinal))
            {
                var af = Path.Combine(actual, Path.GetFileName(gf));
                if (!File.Exists(af)) return new ChannelResult("random", ChannelStatus.Missing, "no run output for " + Path.GetFileName(gf));
                var g = JsonNode.Parse(File.ReadAllText(gf))!.AsObject();
                var a = JsonNode.Parse(File.ReadAllText(af))!.AsObject();
                foreach (var kind in new[] { "value", "range", "onUnitSphere" })
                {
                    var ga = g[kind] as JsonArray; var aa = a[kind] as JsonArray;
                    if (ga == null) continue;
                    if (aa == null) return new ChannelResult("random", ChannelStatus.Fail, $"{Path.GetFileName(gf)}: run has no '{kind}'");
                    for (int i = 0; i < ga.Count; i++)
                    {
                        if (i >= aa.Count || !JsonNode.DeepEquals(ga[i], aa[i]))
                            return new ChannelResult("random", ChannelStatus.Fail, $"seed {g["seed"]} {kind}[{i}]: golden {ga[i]?.ToJsonString()} vs {(i < aa.Count ? aa[i]?.ToJsonString() : "(end)")}");
                        draws++;
                    }
                }
            }
            return new ChannelResult("random", ChannelStatus.Pass, $"{files.Length} seed(s), {draws} draws identical");
        }

        // ---- events: exact order and count, time within a step ---------------------------------------

        public static ChannelResult Events(string golden, string actual, Tolerances tol)
        {
            var g = Lines(Path.Combine(golden, "events.jsonl"));
            var a = Lines(Path.Combine(actual, "events.jsonl"));
            if (MissingEither("events", g, a) is { } m) return m;
            // Kinds (fmod, game, contact) land in the Unity probe one at a time: compare the ordered
            // sequence of the kinds the golden carries, and name the ones it does not (no claim).
            static string KindOf(JsonObject o) => o["kind"]?.ToString() ?? "";
            var kinds = new HashSet<string>(g!.Select(KindOf));
            var unclaimed = a!.Select(KindOf).Where(k => !kinds.Contains(k)).Distinct().OrderBy(k => k, StringComparer.Ordinal).ToList();
            a = a.Where(o => kinds.Contains(KindOf(o))).ToList();
            string note = unclaimed.Count == 0 ? "" : $"; no golden for kind(s) {string.Join(", ", unclaimed)}";
            int n = Math.Min(g.Count, a.Count);
            for (int i = 0; i < n; i++)
            {
                string gk = $"{g[i]["kind"]}:{g[i]["name"]}", ak = $"{a[i]["kind"]}:{a[i]["name"]}";
                if (gk != ak) return new ChannelResult("events", ChannelStatus.Fail, $"event {i}: golden {gk} at {g[i]["t"]} s vs {ak} at {a[i]["t"]} s");
                double dt = Math.Abs(g[i]["t"]!.GetValue<double>() - a[i]["t"]!.GetValue<double>());
                if (dt > tol.EventTimeSeconds + 1e-9)
                    return new ChannelResult("events", ChannelStatus.Fail, $"event {i} {gk}: {dt:0.000} s late/early (bar {tol.EventTimeSeconds} s)");
            }
            if (g.Count != a.Count)
            {
                var extra = g.Count > a.Count ? "golden " + g[n].ToJsonString() : "run " + a[n].ToJsonString();
                return new ChannelResult("events", ChannelStatus.Fail, $"{g.Count} golden events vs {a.Count}; first unmatched: {extra}");
            }
            return new ChannelResult("events", ChannelStatus.Pass, $"{n} events in order ({string.Join(", ", kinds.OrderBy(k => k, StringComparer.Ordinal))}){note}");
        }

        // ---- transforms: within 1e-4 of distance and 0.1 degrees, first 10 s ------------------------

        public static ChannelResult Transforms(string golden, string actual, Tolerances tol)
        {
            var g = Lines(Path.Combine(golden, "transforms.jsonl"));
            var a = Lines(Path.Combine(actual, "transforms.jsonl"));
            if (MissingEither("transforms", g, a) is { } m) return m;
            static string Key(JsonObject o) => $"{o["frame"]}|{o["id"]}";
            var byKey = a!.GroupBy(Key).ToDictionary(x => x.Key, x => x.First());
            double worstPos = 0, worstRot = 0; int compared = 0;
            foreach (var gr in g!)
            {
                if (gr["t"]!.GetValue<double>() > tol.TransformWindowSeconds) continue;
                if (!byKey.TryGetValue(Key(gr), out var ar))
                    return new ChannelResult("transforms", ChannelStatus.Fail, $"frame {gr["frame"]}: {gr["id"]} missing in the run");
                var gp = Vec(gr["p"]); var ap = Vec(ar["p"]);
                double dist = Math.Sqrt(gp.Sum(x => x * x));
                double err = Math.Sqrt(gp.Zip(ap, (x, y) => (x - y) * (x - y)).Sum());
                double bar = tol.PositionRelative * Math.Max(dist, tol.PositionFloorMetres);
                if (err > bar)
                    return new ChannelResult("transforms", ChannelStatus.Fail, $"frame {gr["frame"]} {gr["id"]}: position off by {err:G4} m at {dist:0.0} m from origin (bar {bar:G3} m)");
                double ang = QuatAngleDegrees(Vec(gr["r"]), Vec(ar["r"]));
                if (ang > tol.RotationDegrees)
                    return new ChannelResult("transforms", ChannelStatus.Fail, $"frame {gr["frame"]} {gr["id"]}: rotation off by {ang:0.000} deg (bar {tol.RotationDegrees})");
                worstPos = Math.Max(worstPos, bar > 0 ? err / bar : 0); worstRot = Math.Max(worstRot, ang); compared++;
            }
            return new ChannelResult("transforms", ChannelStatus.Pass, $"{compared} samples; worst position {worstPos:P0} of bar, worst rotation {worstRot:0.000} deg");
        }

        static double[] Vec(JsonNode? n) => ((JsonArray)n!).Select(x => x!.GetValue<double>()).ToArray();

        public static double QuatAngleDegrees(double[] q1, double[] q2)
        {
            double dot = Math.Abs(q1[0] * q2[0] + q1[1] * q2[1] + q1[2] * q2[2] + q1[3] * q2[3]);
            double n1 = Math.Sqrt(q1.Sum(x => x * x)), n2 = Math.Sqrt(q2.Sum(x => x * x));
            dot = Math.Min(1.0, dot / (n1 * n2));
            return 2.0 * Math.Acos(dot) * 180.0 / Math.PI;
        }

        // ---- ui: every RectTransform's screen rect, per screen and resolution ------------------------

        /// <summary>
        /// Every golden ui/&lt;view&gt;_&lt;WxH&gt;.jsonl (written by the Unity capture and by the engine's
        /// <c>ui_sweep</c>) against the run's file of the same name: the same set of active
        /// elements (by path), each corner within <see cref="Tolerances.UiRectPixels"/>, and for text
        /// the same overflow verdict and a font size within <see cref="Tolerances.UiFontSize"/>.
        /// </summary>
        public static ChannelResult Ui(string golden, string actual, Tolerances tol)
        {
            var gdir = Path.Combine(golden, "ui");
            if (!Directory.Exists(gdir) || Directory.GetFiles(gdir, "*.jsonl").Length == 0) return new ChannelResult("ui", ChannelStatus.Missing, "no golden");
            var adir = Path.Combine(actual, "ui");
            if (!Directory.Exists(adir)) return new ChannelResult("ui", ChannelStatus.Missing, "no run output");
            int files = 0, elements = 0; double worst = 0; string worstAt = "";
            foreach (var gf in Directory.GetFiles(gdir, "*.jsonl").OrderBy(f => f, StringComparer.Ordinal))
            {
                var name = Path.GetFileName(gf);
                var af = Path.Combine(adir, name);
                if (!File.Exists(af)) return new ChannelResult("ui", ChannelStatus.Fail, $"{name}: no rect dump in the run");
                var g = Lines(gf)!; var a = Lines(af)!;
                var byPath = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
                foreach (var o in a) byPath.TryAdd(o["path"]!.ToString(), o);
                var goldenPaths = new HashSet<string>(StringComparer.Ordinal);
                foreach (var ge in g)
                {
                    var path = ge["path"]!.ToString();
                    goldenPaths.Add(path);
                    if (!byPath.TryGetValue(path, out var ae))
                        return new ChannelResult("ui", ChannelStatus.Fail, $"{name}: {path} is active in the golden, not in the run");
                    double err = new[] { "x0", "y0", "x1", "y1" }.Max(k => Math.Abs(ge[k]!.GetValue<double>() - ae[k]!.GetValue<double>()));
                    if (err > tol.UiRectPixels + 1e-9)
                        return new ChannelResult("ui", ChannelStatus.Fail, $"{name}: {path} rect off by {err:0.0} px: golden {RectText(ge)} vs {RectText(ae)}");
                    if (ge["overflow"] is { } go && ae["overflow"] is { } ao && go.GetValue<bool>() != ao.GetValue<bool>())
                        return new ChannelResult("ui", ChannelStatus.Fail, $"{name}: {path} text overflow golden {go} vs {ao}");
                    if (ge["fontSize"] is { } gs && ae["fontSize"] is { } fs && Math.Abs(gs.GetValue<double>() - fs.GetValue<double>()) > tol.UiFontSize + 1e-9)
                        return new ChannelResult("ui", ChannelStatus.Fail, $"{name}: {path} font size golden {gs} vs {fs}");
                    if (err > worst) { worst = err; worstAt = $"{name} {path}"; }
                    elements++;
                }
                var extra = a.Select(o => o["path"]!.ToString()).FirstOrDefault(p => !goldenPaths.Contains(p));
                if (extra != null) return new ChannelResult("ui", ChannelStatus.Fail, $"{name}: {extra} is active in the run, not in the golden");
                files++;
            }
            return new ChannelResult("ui", ChannelStatus.Pass, $"{files} screen(s), {elements} rects; worst corner {worst:0.0} px{(worstAt.Length > 0 ? " (" + worstAt + ")" : "")}");
        }

        static string RectText(JsonObject o) => $"({o["x0"]},{o["y0"]})-({o["x1"]},{o["y1"]})";

        // ---- frames: SSIM per frame ----------------------------------------------------------------------

        /// <summary>
        /// Every golden frames/*.png against the run's frame of the same name. Frames under
        /// frames/ui/ use the UI bar. frames/masks.json ({"f00003.png": [[x,y,w,h], ...]}) blanks
        /// regions in both images - particle regions until C2.
        /// </summary>
        public static ChannelResult Frames(string golden, string actual, Tolerances tol)
        {
            var gdir = Path.Combine(golden, "frames");
            if (!Directory.Exists(gdir)) return new ChannelResult("frames", ChannelStatus.Missing, "no golden");
            var adir = Path.Combine(actual, "frames");
            if (!Directory.Exists(adir)) return new ChannelResult("frames", ChannelStatus.Missing, "no run output");
            var masks = File.Exists(Path.Combine(gdir, "masks.json")) ? JsonNode.Parse(File.ReadAllText(Path.Combine(gdir, "masks.json")))!.AsObject() : new JsonObject();
            double worst = 1; string worstName = ""; int n = 0;
            foreach (var gf in Directory.GetFiles(gdir, "*.png", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.Ordinal))
            {
                var rel = Path.GetRelativePath(gdir, gf);
                var af = Path.Combine(adir, rel);
                if (!File.Exists(af)) return new ChannelResult("frames", ChannelStatus.Fail, $"{rel}: no frame in the run");
                var gi = Png.Decode(File.ReadAllBytes(gf)); var ai = Png.Decode(File.ReadAllBytes(af));
                if (gi.Width != ai.Width || gi.Height != ai.Height)
                    return new ChannelResult("frames", ChannelStatus.Fail, $"{rel}: {gi.Width}x{gi.Height} golden vs {ai.Width}x{ai.Height}");
                if (masks[rel.Replace('\\', '/')] is JsonArray rects)
                    foreach (var r in rects) { var v = Vec(r); Mask(gi, v); Mask(ai, v); }
                double s = Ssim(gi, ai);
                bool ui = rel.Replace('\\', '/').StartsWith("ui/");
                double bar = ui ? tol.SsimUi : tol.SsimGameplay;
                if (s < bar) return new ChannelResult("frames", ChannelStatus.Fail, $"{rel}: SSIM {s:0.0000} < {bar}");
                if (s < worst) { worst = s; worstName = rel; }
                n++;
            }
            return new ChannelResult("frames", ChannelStatus.Pass, $"{n} frames; lowest SSIM {worst:0.0000}{(n > 0 ? " (" + worstName + ")" : "")}");
        }

        static void Mask(Png.Image img, double[] r)
        {
            int x0 = Math.Max(0, (int)r[0]), y0 = Math.Max(0, (int)r[1]);
            int x1 = Math.Min(img.Width, (int)(r[0] + r[2])), y1 = Math.Min(img.Height, (int)(r[1] + r[3]));
            for (int y = y0; y < y1; y++)
                for (int x = x0; x < x1; x++)
                {
                    int i = (y * img.Width + x) * 4;
                    img.Rgba[i] = img.Rgba[i + 1] = img.Rgba[i + 2] = 0; img.Rgba[i + 3] = 255;
                }
        }

        /// <summary>Mean SSIM over 8x8 windows (stride 4) of BT.601 luma; constants from Wang et al. 2004 (K1 0.01, K2 0.03, L 255).</summary>
        public static double Ssim(Png.Image a, Png.Image b)
        {
            if (a.Width != b.Width || a.Height != b.Height) throw new ArgumentException("size mismatch");
            int w = a.Width, h = a.Height;
            var la = Luma(a); var lb = Luma(b);
            const double C1 = 6.5025, C2 = 58.5225; // (0.01*255)^2, (0.03*255)^2
            const int W = 8, S = 4;
            double total = 0; int windows = 0;
            for (int y = 0; y + W <= h; y += S)
                for (int x = 0; x + W <= w; x += S)
                {
                    double ma = 0, mb = 0;
                    for (int j = 0; j < W; j++) for (int i = 0; i < W; i++) { int k = (y + j) * w + x + i; ma += la[k]; mb += lb[k]; }
                    ma /= W * W; mb /= W * W;
                    double va = 0, vb = 0, cov = 0;
                    for (int j = 0; j < W; j++) for (int i = 0; i < W; i++)
                    {
                        int k = (y + j) * w + x + i;
                        double da = la[k] - ma, db = lb[k] - mb;
                        va += da * da; vb += db * db; cov += da * db;
                    }
                    va /= W * W - 1; vb /= W * W - 1; cov /= W * W - 1;
                    total += (2 * ma * mb + C1) * (2 * cov + C2) / ((ma * ma + mb * mb + C1) * (va + vb + C2));
                    windows++;
                }
            return windows == 0 ? 1.0 : total / windows;
        }

        static double[] Luma(Png.Image img)
        {
            var l = new double[img.Width * img.Height];
            for (int i = 0; i < l.Length; i++)
                l[i] = 0.299 * img.Rgba[i * 4] + 0.587 * img.Rgba[i * 4 + 1] + 0.114 * img.Rgba[i * 4 + 2];
            return l;
        }
    }

    /// <summary>Minimal PNG decoder: 8-bit greyscale, RGB, grey+alpha and RGBA, non-interlaced (what Unity and MiniPng write).</summary>
    public static class Png
    {
        public sealed class Image
        {
            public int Width, Height;
            public byte[] Rgba = Array.Empty<byte>();
        }

        public static Image Decode(byte[] data)
        {
            if (data.Length < 8 || data[0] != 137 || data[1] != 80) throw new InvalidDataException("not a PNG");
            int pos = 8, width = 0, height = 0, depth = 0, colour = 0, interlace = 0;
            var idat = new MemoryStream();
            while (pos + 8 <= data.Length)
            {
                int len = BE(data, pos); string type = System.Text.Encoding.ASCII.GetString(data, pos + 4, 4);
                int body = pos + 8;
                if (type == "IHDR") { width = BE(data, body); height = BE(data, body + 4); depth = data[body + 8]; colour = data[body + 9]; interlace = data[body + 12]; }
                else if (type == "IDAT") idat.Write(data, body, len);
                else if (type == "IEND") break;
                pos = body + len + 4;
            }
            if (depth != 8 || interlace != 0) throw new InvalidDataException($"unsupported PNG (depth {depth}, interlace {interlace})");
            int channels = colour switch { 0 => 1, 2 => 3, 4 => 2, 6 => 4, _ => throw new InvalidDataException($"unsupported PNG colour type {colour}") };
            idat.Position = 0;
            using var z = new ZLibStream(idat, CompressionMode.Decompress);
            var raw = new MemoryStream(); z.CopyTo(raw);
            var src = raw.ToArray();
            int stride = width * channels;
            var cur = new byte[stride]; var prev = new byte[stride];
            var img = new Image { Width = width, Height = height, Rgba = new byte[width * height * 4] };
            int p = 0;
            for (int y = 0; y < height; y++)
            {
                byte filter = src[p++];
                Buffer.BlockCopy(src, p, cur, 0, stride); p += stride;
                for (int i = 0; i < stride; i++)
                {
                    int left = i >= channels ? cur[i - channels] : 0, up = prev[i], ul = i >= channels ? prev[i - channels] : 0;
                    cur[i] = filter switch
                    {
                        0 => cur[i],
                        1 => (byte)(cur[i] + left),
                        2 => (byte)(cur[i] + up),
                        3 => (byte)(cur[i] + ((left + up) >> 1)),
                        4 => (byte)(cur[i] + Paeth(left, up, ul)),
                        _ => throw new InvalidDataException("bad PNG filter " + filter),
                    };
                }
                for (int x = 0; x < width; x++)
                {
                    int o = (y * width + x) * 4, s = x * channels;
                    switch (channels)
                    {
                        case 1: img.Rgba[o] = img.Rgba[o + 1] = img.Rgba[o + 2] = cur[s]; img.Rgba[o + 3] = 255; break;
                        case 2: img.Rgba[o] = img.Rgba[o + 1] = img.Rgba[o + 2] = cur[s]; img.Rgba[o + 3] = cur[s + 1]; break;
                        case 3: img.Rgba[o] = cur[s]; img.Rgba[o + 1] = cur[s + 1]; img.Rgba[o + 2] = cur[s + 2]; img.Rgba[o + 3] = 255; break;
                        default: Buffer.BlockCopy(cur, s, img.Rgba, o, 4); break;
                    }
                }
                (prev, cur) = (cur, prev);
            }
            return img;
        }

        static int Paeth(int a, int b, int c)
        {
            int p = a + b - c, pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
            return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
        }

        static int BE(byte[] d, int i) => (d[i] << 24) | (d[i + 1] << 16) | (d[i + 2] << 8) | d[i + 3];
    }
}
