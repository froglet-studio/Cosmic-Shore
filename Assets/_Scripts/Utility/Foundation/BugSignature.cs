using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// Deterministic bug fingerprints — the shared core of the editor Bug Ledger
    /// (FrogletTools ▸ Diagnostics, see <c>Docs/DIAGNOSTICS.md</c>) and, later, of the in-game
    /// crash/bug reporter (which will write the same ids into UGS player data, so device hits can
    /// merge into the same ledger issues).
    ///
    /// <para>Deliberately RUNTIME-SAFE: pure C# + <see cref="LogType"/> only, no UnityEditor, no
    /// statics with lifecycle, callable from any thread. Everything here must stay a pure function
    /// — two machines (or a device and the editor) hashing the same bug must produce the same id,
    /// which is the whole contract. Covered by <c>BugSignatureTests</c> (edit mode).</para>
    /// </summary>
    public static class BugSignature
    {
        /// <summary>
        /// Id for a captured error: <c>E-</c> + 10 hex of MD5 over
        /// (log type | normalized message | normalized top user frame). Digits, hex runs and
        /// machine-local path prefixes are collapsed so counts, instance ids, positions and
        /// checkout locations don't split one bug into many.
        /// </summary>
        public static string ErrorId(string condition, string stack, LogType type, out string signature)
        {
            signature = $"{type}|{NormalizeText(condition, 300)}|{TopUserFrame(stack)}";
            return "E-" + Hash10(signature);
        }

        /// <summary>
        /// Id for a finding an editor TOOL reports (auditors, validators, the crash detector's
        /// File Bug): <c>T-</c> + 10 hex over (tool | normalized title). Stable across runs and
        /// machines, so re-running a tool updates the same issue instead of minting a duplicate.
        /// </summary>
        public static string ToolId(string toolName, string title, out string signature)
        {
            signature = $"Tool|{toolName}|{NormalizeText(title, 200)}";
            return "T-" + Hash10(signature);
        }

        /// <summary>First line, trimmed and capped, with hex runs collapsed to <c>0x#</c> and
        /// digit runs to <c>#</c>.</summary>
        public static string NormalizeText(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return "";
            int newline = s.IndexOf('\n');
            if (newline >= 0) s = s[..newline];
            s = s.Trim();
            if (s.Length > max) s = s[..max];

            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '0' && i + 1 < s.Length && (s[i + 1] == 'x' || s[i + 1] == 'X'))
                {
                    int j = i + 2;
                    while (j < s.Length && Uri.IsHexDigit(s[j])) j++;
                    if (j > i + 2)
                    {
                        sb.Append("0x#");
                        i = j - 1;
                        continue;
                    }
                }
                if (char.IsDigit(c))
                {
                    while (i + 1 < s.Length && char.IsDigit(s[i + 1])) i++;
                    sb.Append('#');
                    continue;
                }
                sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>
        /// The first CosmicShore frame of a stack (else the first frame at all), normalized, with
        /// any machine-local absolute path stripped back to <c>Assets/…</c>. Both frame formats
        /// carry a source location — mono-style <c>… in &lt;path&gt;:line</c> and unity-style
        /// <c>… (at &lt;path&gt;:line)</c> — and either may be absolute on one machine and
        /// repo-relative on another, so both are cut back.
        /// </summary>
        public static string TopUserFrame(string stack)
        {
            if (string.IsNullOrEmpty(stack)) return "";
            string firstNonEmpty = null;
            string pick = null;
            foreach (var raw in stack.Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0) continue;
                firstNonEmpty ??= line;
                if (line.Contains("CosmicShore", StringComparison.Ordinal) &&
                    !line.Contains("BugLedger", StringComparison.Ordinal) &&
                    !line.Contains("BugSignature", StringComparison.Ordinal) &&
                    !line.Contains("CrashDetector", StringComparison.Ordinal))
                {
                    pick = line;
                    break;
                }
            }
            pick ??= firstNonEmpty;
            if (pick == null) return "";

            pick = pick.Replace('\\', '/');
            pick = StripPathAfterMarker(pick, " in ");
            pick = StripPathAfterMarker(pick, "(at ");
            return NormalizeText(pick, 240);
        }

        static string StripPathAfterMarker(string line, string marker)
        {
            int idx = line.LastIndexOf(marker, StringComparison.Ordinal);
            if (idx < 0) return line;
            int pathStart = idx + marker.Length;
            var tail = line[pathStart..];
            int assetsIdx = tail.IndexOf("Assets/", StringComparison.OrdinalIgnoreCase);
            if (assetsIdx == 0) return line;                              // already repo-relative
            if (assetsIdx > 0) return line[..pathStart] + tail[assetsIdx..];
            return line[..idx];   // no Assets/ segment (packages, il2cpp) — drop the alien path
        }

        static string Hash10(string s)
        {
            using var md5 = MD5.Create();
            var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(s));
            var sb = new StringBuilder(10);
            for (int i = 0; i < 5; i++) sb.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
            return sb.ToString();
        }
    }
}
