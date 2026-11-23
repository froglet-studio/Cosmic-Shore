using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace CosmicShore.Utility.PerformanceBenchmark
{
    /// <summary>
    /// The pure half of the phone's play-test logs (Docs/perf/DEVICE_LOGS.md): no Unity types and no build guard, so the
    /// edit-mode tests hold it. <see cref="DeviceLogCapture"/> calls these from a development build.
    /// </summary>
    public static class DeviceLogFormat
    {
        static readonly Regex TokenShape = new("(ghp_|github_pat_)[A-Za-z0-9_]+", RegexOptions.CultureInvariant);

        /// <summary>True when the text has the shape of one GitHub personal access token and nothing else.</summary>
        public static bool LooksLikeToken(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            string t = text.Trim();
            if (t.Length < 20 || t.Length > 300) return false;
            if (!t.StartsWith("ghp_", StringComparison.Ordinal) && !t.StartsWith("github_pat_", StringComparison.Ordinal)) return false;
            foreach (char c in t)
                if (!char.IsLetterOrDigit(c) && c != '_') return false;
            return true;
        }

        /// <summary>The text with anything token-shaped blanked: the console logs every line it runs, and logs get uploaded.</summary>
        public static string MaskTokens(string text) =>
            string.IsNullOrEmpty(text) ? text : TokenShape.Replace(text, "[token removed]");

        /// <summary>A name safe inside a branch and a folder path: lowercase letters, digits and dashes.</summary>
        public static string SafeName(string text, string fallback)
        {
            if (string.IsNullOrWhiteSpace(text)) return fallback;
            var sb = new StringBuilder(text.Length);
            foreach (char c in text.Trim().ToLowerInvariant())
            {
                if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-') sb.Append(c);
                else if (sb.Length > 0 && sb[sb.Length - 1] != '-') sb.Append('-');
            }
            string s = sb.ToString().Trim('-');
            return s.Length == 0 ? fallback : s.Length > 40 ? s.Substring(0, 40) : s;
        }

        /// <summary>The value at <paramref name="fraction"/> of an ascending-sorted range.</summary>
        public static float Percentile(float[] sorted, int count, float fraction)
        {
            if (sorted == null || count <= 0) return 0f;
            int i = (int)(count * fraction);
            return sorted[i < 0 ? 0 : i >= count ? count - 1 : i];
        }

        /// <summary>One line for the phone's notification and the commit message.</summary>
        public static string Headline(double minutes, int sceneCount, float worstFps, string worstScene, int errorKinds, int marks)
        {
            var c = CultureInfo.InvariantCulture;
            string worst = string.IsNullOrEmpty(worstScene) ? "" : string.Format(c, ", worst {0:F0} fps in {1}", worstFps, worstScene);
            string marked = marks > 0 ? string.Format(c, ", {0} marked", marks) : "";
            return string.Format(c, "{0:F0} min, {1} {2}{3}, {4} error {5}{6}", minutes, sceneCount, sceneCount == 1 ? "scene" : "scenes",
                worst, errorKinds, errorKinds == 1 ? "kind" : "kinds", marked);
        }
    }
}
