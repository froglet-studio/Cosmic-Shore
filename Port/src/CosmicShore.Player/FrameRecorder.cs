using System;
using System.Collections.Generic;
using System.IO;
using CosmicShore.Engine;

namespace CosmicShore.Player
{
    /// <summary>
    /// --record DIR:FROM-TO[:EVERY] — saves every EVERYth presented frame in [FROM, TO] as a
    /// numbered PNG and appends "file, game time" to DIR/frames.csv. Scripted runs step the game
    /// by a fixed 1/60 s, so the game-time stamps let a video play back at true game speed however
    /// slowly the frames were drawn. Repeatable; each range writes into its own DIR.
    /// </summary>
    public static class FrameRecorder
    {
        sealed class Range { public string Dir; public int From, To, Every; public int Count; }
        static readonly List<Range> s_ranges = new();

        public static int LastFrame { get; private set; } = -1;

        public static bool TryAdd(string spec)
        {
            var parts = spec.Split(':');
            if (parts.Length < 2) return false;
            var span = parts[1].Split('-');
            if (span.Length != 2 || !int.TryParse(span[0], out int from) || !int.TryParse(span[1], out int to) || to < from) return false;
            int every = 1;
            if (parts.Length > 2 && (!int.TryParse(parts[2], out every) || every < 1)) return false;
            Directory.CreateDirectory(parts[0]);
            File.WriteAllText(Path.Combine(parts[0], "frames.csv"), "file,frame,time\n");
            s_ranges.Add(new Range { Dir = parts[0], From = from, To = to, Every = every });
            LastFrame = Math.Max(LastFrame, to);
            return true;
        }

        /// <summary>True when <paramref name="frame"/> lies in a recorded range (so it must be drawn).</summary>
        public static bool Wants(int frame)
        {
            foreach (var r in s_ranges)
                if (frame >= r.From && frame <= r.To && (frame - r.From) % r.Every == 0) return true;
            return false;
        }

        public static bool TryPath(int frame, out string path)
        {
            foreach (var r in s_ranges)
            {
                if (frame < r.From || frame > r.To || (frame - r.From) % r.Every != 0) continue;
                string name = $"f{r.Count++:D5}.png";
                path = Path.Combine(r.Dir, name);
                File.AppendAllText(Path.Combine(r.Dir, "frames.csv"), $"{name},{frame},{Time.time.ToString("R", System.Globalization.CultureInfo.InvariantCulture)}\n");
                return true;
            }
            path = null;
            return false;
        }
    }
}
