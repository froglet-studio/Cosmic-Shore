using System;
using System.Collections.Generic;
using System.IO;
using CosmicShore.Engine;

namespace CosmicShore.Player
{
    /// <summary>
    /// --record DIR:FROM-TO[:EVERY[:ACCUM]] — saves every EVERYth presented frame in [FROM, TO] as a
    /// numbered PNG and appends "file, game time" to DIR/frames.csv. Scripted runs step the game
    /// by a fixed step (1/60 s, or 1/<c>--step-hz</c>), so the game-time stamps let a video play
    /// back at true game speed however slowly the frames were drawn. Repeatable; each range writes
    /// into its own DIR.
    ///
    /// ACCUM (default 1) is temporal supersampling: each output frame is the average, in linear
    /// light, of ACCUM consecutive sub-frames (so with <c>--step-hz 240</c>, EVERY 4 and ACCUM 4 the
    /// video is 60 fps with a full 180-degree-shutter motion blur, every sub-frame rendered at the
    /// renderer's full quality). The music video director records this way: nothing is real time.
    /// </summary>
    public static class FrameRecorder
    {
        sealed class Range { public string Dir; public int From, To, Every, Accum = 1; public int Count; public float[] Sum; public int Summed; }
        static readonly List<Range> s_ranges = new();

        public static int LastFrame { get; private set; } = -1;

        public static bool TryAdd(string spec)
        {
            var parts = spec.Split(':');
            if (parts.Length < 2) return false;
            var span = parts[1].Split('-');
            if (span.Length != 2 || !int.TryParse(span[0], out int from) || !int.TryParse(span[1], out int to) || to < from) return false;
            int every = 1, accum = 1;
            if (parts.Length > 2 && (!int.TryParse(parts[2], out every) || every < 1)) return false;
            if (parts.Length > 3 && (!int.TryParse(parts[3], out accum) || accum < 1 || accum > every)) return false;
            Directory.CreateDirectory(parts[0]);
            File.WriteAllText(Path.Combine(parts[0], "frames.csv"), "file,frame,time\n");
            s_ranges.Add(new Range { Dir = parts[0], From = from, To = to, Every = every, Accum = accum });
            LastFrame = Math.Max(LastFrame, to);
            return true;
        }

        /// <summary>True when <paramref name="frame"/> lies in a recorded range (so it must be drawn): an output frame or one of its accumulated sub-frames.</summary>
        public static bool Wants(int frame)
        {
            foreach (var r in s_ranges)
                if (frame >= r.From && frame <= r.To && (frame - r.From) % r.Every < r.Accum) return true;
            return false;
        }

        /// <summary>
        /// The range that wants this presented frame, if any: a plain output frame (ACCUM 1) gets its path at
        /// once; an accumulated one is summed and the path comes back on the last sub-frame.
        /// </summary>
        public static bool TryPath(int frame, out string path)
        {
            foreach (var r in s_ranges)
            {
                if (frame < r.From || frame > r.To || (frame - r.From) % r.Every != 0 || r.Accum != 1) continue;
                path = Next(r, frame);
                return true;
            }
            path = null;
            return false;
        }

        static string Next(Range r, int frame)
        {
            string name = $"f{r.Count++:D5}.png";
            File.AppendAllText(Path.Combine(r.Dir, "frames.csv"), $"{name},{frame},{Time.time.ToString("R", System.Globalization.CultureInfo.InvariantCulture)}\n");
            return Path.Combine(r.Dir, name);
        }

        /// <summary>True when <paramref name="frame"/> is a sub-frame of an accumulated output frame (the caller reads the pixels back and calls <see cref="Accumulate"/>).</summary>
        public static bool WantsSubFrame(int frame)
        {
            foreach (var r in s_ranges)
                if (frame >= r.From && frame <= r.To && r.Accum > 1 && (frame - r.From) % r.Every < r.Accum) return true;
            return false;
        }

        static readonly float[] s_toLinear = BuildToLinear();
        static float[] BuildToLinear()
        {
            var t = new float[256];
            for (int i = 0; i < 256; i++) { float c = i / 255f; t[i] = c <= 0.04045f ? c / 12.92f : MathF.Pow((c + 0.055f) / 1.055f, 2.4f); }
            return t;
        }
        static byte ToSrgb(float l)
        {
            l = Math.Clamp(l, 0f, 1f);
            float c = l <= 0.0031308f ? l * 12.92f : 1.055f * MathF.Pow(l, 1f / 2.4f) - 0.055f;
            return (byte)Math.Clamp((int)MathF.Round(c * 255f), 0, 255);
        }

        /// <summary>
        /// Adds a rendered sub-frame (RGBA8, bottom-up as GL reads it) to its output frame's sum in linear
        /// light; on the last sub-frame returns the averaged pixels (sRGB, same layout) and the path to write.
        /// </summary>
        public static bool Accumulate(int frame, byte[] pixels, int w, int h, out byte[] averaged, out string path)
        {
            averaged = null; path = null;
            foreach (var r in s_ranges)
            {
                if (frame < r.From || frame > r.To || r.Accum <= 1) continue;
                int k = (frame - r.From) % r.Every;
                if (k >= r.Accum) continue;
                int n = w * h * 4;
                if (r.Sum == null || r.Sum.Length != n || k == 0) { r.Sum = r.Sum != null && r.Sum.Length == n ? r.Sum : new float[n]; Array.Clear(r.Sum, 0, n); r.Summed = 0; }
                for (int i = 0; i < n; i += 4)
                {
                    r.Sum[i] += s_toLinear[pixels[i]]; r.Sum[i + 1] += s_toLinear[pixels[i + 1]]; r.Sum[i + 2] += s_toLinear[pixels[i + 2]];
                }
                r.Summed++;
                if (k != r.Accum - 1) return false;
                averaged = new byte[n];
                float inv = 1f / r.Summed;
                for (int i = 0; i < n; i += 4)
                {
                    averaged[i] = ToSrgb(r.Sum[i] * inv); averaged[i + 1] = ToSrgb(r.Sum[i + 1] * inv); averaged[i + 2] = ToSrgb(r.Sum[i + 2] * inv); averaged[i + 3] = 255;
                }
                path = Next(r, frame);
                return true;
            }
            return false;
        }
    }
}
