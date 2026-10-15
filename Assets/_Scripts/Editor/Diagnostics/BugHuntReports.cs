using System;
using System.Collections.Generic;
using System.IO;
using CosmicShore.Utility.PerformanceBenchmark;
using UnityEngine;

namespace CosmicShore.Editor
{
    /// <summary>
    /// The diagnostic FILES a collaborator's machine produces, gathered for the Bug Hunt check-in
    /// (<see cref="BugHuntCheckIn"/>) and the after-play auto-publish, so the daily bug sweep can read
    /// them without anyone attaching anything by hand. Three producers, all already running:
    ///
    /// <list type="bullet">
    /// <item><b>redlog</b> — every red Console entry of a play session, one file per session
    /// (<c>Logs/RedConsole/redconsole_*.log</c>, Yash's <c>RedConsoleLog</c>). The full-context twin of
    /// the ledger's one-issue-per-signature view.</item>
    /// <item><b>crash</b> — the Crash Detector's abnormal-exit reports (<c>Logs/CrashDetector/Crash-*.log</c>).</item>
    /// <item><b>perf</b> — the DiagnosticsHUD's JSON reports and captures
    /// (<c>Documents/CosmicShore Diagnostics/*.json</c>: <c>capture_</c>, <c>diag_</c>, <c>prof_</c>,
    /// <c>cells_</c>).</item>
    /// </list>
    ///
    /// Copies land in <c>BugLedger/shared/reports/&lt;user&gt;/&lt;kind&gt;/</c> and are committed by the
    /// ledger's own scoped publish — never anything outside <c>BugLedger/</c>. A file is "new" when its
    /// copy does not exist yet, so nothing is uploaded twice and no machine-local bookkeeping can drift.
    /// Bounded on purpose: recent files only, a size cap per file and a count cap per share, newest
    /// first — the repo carries evidence, not an archive.
    /// </summary>
    public static class BugHuntReports
    {
        const long MaxFileBytes = 512 * 1024;
        const int MaxFilesPerShare = 12;
        const int MaxAgeDays = 14;

        /// <summary>One planned copy: absolute source and destination.</summary>
        public readonly struct Copy
        {
            public readonly string Source;
            public readonly string Destination;
            public readonly string Kind;

            public Copy(string source, string destination, string kind)
            {
                Source = source;
                Destination = destination;
                Kind = kind;
            }
        }

        /// <summary><c>BugLedger/shared/reports</c> — tracked, published only through the ledger.</summary>
        public static string SharedReportsDir
            => BugLedger.RootDir == null ? null : Path.Combine(BugLedger.RootDir, "shared", "reports");

        /// <summary>
        /// Main thread only (resolves <c>DiagnosticsHUD.OutputDirectory</c>, which may fall back to
        /// <c>Application.persistentDataPath</c>). Returns the not-yet-shared report files, newest first,
        /// capped. Never throws — a missing or unreadable folder simply contributes nothing.
        /// </summary>
        public static List<Copy> Plan()
        {
            var planned = new List<Copy>();
            var root = SharedReportsDir;
            if (root == null) return planned;

            string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
            string userDir = Path.Combine(root, Slug(Environment.UserName));
            var cutoff = DateTime.UtcNow.AddDays(-MaxAgeDays);
            var candidates = new List<(FileInfo file, string kind)>();

            Collect(candidates, Path.Combine(projectRoot, "Logs", "RedConsole"), "redconsole_*.log", "redlog", cutoff);
            Collect(candidates, Path.Combine(projectRoot, "Logs", "CrashDetector"), "Crash-*.log", "crash", cutoff);
            string perfDir = null;
            try { perfDir = DiagnosticsHUD.OutputDirectory; } catch { /* no Documents folder: nothing to share */ }
            if (perfDir != null) Collect(candidates, perfDir, "*.json", "perf", cutoff);

            candidates.Sort((a, b) => b.file.LastWriteTimeUtc.CompareTo(a.file.LastWriteTimeUtc));
            foreach (var (file, kind) in candidates)
            {
                if (planned.Count >= MaxFilesPerShare) break;
                var destination = Path.Combine(userDir, kind, file.Name);
                if (File.Exists(destination)) continue;
                planned.Add(new Copy(file.FullName, destination, kind));
            }
            return planned;
        }

        /// <summary>Any thread. Copies each planned file; one unreadable file never stops the rest.</summary>
        public static void CopyAll(List<Copy> copies)
        {
            foreach (var copy in copies)
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(copy.Destination)!);
                    File.Copy(copy.Source, copy.Destination, overwrite: false);
                }
                catch { /* locked (a redlog still being written) or vanished: next share retries it */ }
            }
        }

        /// <summary>Human summary, e.g. "2 redlog, 1 perf".</summary>
        public static string Describe(List<Copy> copies)
        {
            int redlog = 0, crash = 0, perf = 0;
            foreach (var c in copies)
            {
                switch (c.Kind)
                {
                    case "redlog": redlog++; break;
                    case "crash": crash++; break;
                    default: perf++; break;
                }
            }
            var parts = new List<string>(3);
            if (redlog > 0) parts.Add($"{redlog} redlog session(s)");
            if (crash > 0) parts.Add($"{crash} crash report(s)");
            if (perf > 0) parts.Add($"{perf} performance/diagnostic JSON(s)");
            return parts.Count == 0 ? "no report files" : string.Join(", ", parts);
        }

        static void Collect(List<(FileInfo, string)> into, string dir, string pattern, string kind, DateTime cutoff)
        {
            try
            {
                if (!Directory.Exists(dir)) return;
                foreach (var path in Directory.GetFiles(dir, pattern))
                {
                    var info = new FileInfo(path);
                    if (info.Length == 0 || info.Length > MaxFileBytes) continue;
                    if (info.LastWriteTimeUtc < cutoff) continue;
                    into.Add((info, kind));
                }
            }
            catch { /* unreadable folder contributes nothing */ }
        }

        static string Slug(string s)
        {
            if (string.IsNullOrEmpty(s)) return "unknown";
            var chars = s.ToLowerInvariant().ToCharArray();
            for (int i = 0; i < chars.Length; i++)
                if (!char.IsLetterOrDigit(chars[i]) && chars[i] != '-') chars[i] = '-';
            return new string(chars);
        }
    }
}
